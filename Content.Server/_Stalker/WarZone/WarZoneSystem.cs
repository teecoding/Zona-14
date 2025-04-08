using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Content.Server._Stalker.WarZone;
using Content.Shared._Stalker.WarZone.Requirenments;
using Content.Server.Database;
using Content.Shared._Stalker.WarZone;
using Content.Shared.Physics;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Physics.Events;

using Content.Shared.Physics;
using Content.Shared._Stalker.Bands;
using Content.Shared.NPC.Prototypes;

namespace Content.Server._Stalker.WarZone;

public sealed partial class WarZoneSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IServerDbManager _dbManager = default!;

    private readonly Dictionary<EntityUid, CaptureState> _activeCaptures = new();
    private readonly Dictionary<EntityUid, TimeSpan> _lastRewardTimes = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WarZoneComponent, StartCollideEvent>(OnStartCollide);
        SubscribeLocalEvent<WarZoneComponent, EndCollideEvent>(OnEndCollide);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _gameTiming.CurTime;

        foreach (var (zone, state) in _activeCaptures)
        {
            _ = UpdateCaptureAsync(zone, state, now);
        }

        foreach (var (zone, lastRewardTime) in _lastRewardTimes)
        {
            DistributeRewards(zone, lastRewardTime, now);
        }
    }

    private async Task UpdateCaptureAsync(EntityUid zone, CaptureState state, TimeSpan now)
    {
        var frameTimeSec = (float)_gameTiming.FrameTime.TotalSeconds;

        if (state.PresentBandIds.Count > 1 || state.PresentFactionIds.Count > 1)
        {
            ResetAllRequirements(zone);
            return;
        }

        if (state.PresentBandIds.Count == 0 && state.PresentFactionIds.Count == 0)
        {
            ResetAllRequirements(zone);
            return;
        }

        int? attackerBand = null;
        int? attackerFaction = null;

        if (state.PresentBandIds.Count == 1)
            attackerBand = GetFirst(state.PresentBandIds);
        if (state.PresentFactionIds.Count == 1)
            attackerFaction = GetFirst(state.PresentFactionIds);

        if ((attackerBand.HasValue && attackerBand == state.DefendingBandId) ||
            (attackerFaction.HasValue && attackerFaction == state.DefendingFactionId))
        {
            ResetAllRequirements(zone);
            return;
        }

        if (!_entityManager.TryGetComponent(zone, out WarZoneComponent? wzComp) ||
            !_prototypeManager.TryIndex<STWarZonePrototype>(wzComp.ZoneProto, out var wzProto))
            return;

        var ownerships = new Dictionary<ProtoId<STWarZonePrototype>, (int? BandId, int? FactionId)>();

        if (wzProto.Requirements != null)
        {
            var requiredZoneIds = new HashSet<ProtoId<STWarZonePrototype>>();

            foreach (var req in wzProto.Requirements)
            {
                if (req is ZoneOwnershipRequirenment zoneReq)
                {
                    foreach (var rid in zoneReq.RequiredZones)
                        requiredZoneIds.Add(rid);
                }
            }

            foreach (var rid in requiredZoneIds)
            {
                var ownership = await _dbManager.GetStalkerWarOwnershipAsync(rid);
                if (ownership != null)
                    ownerships[rid] = (ownership.BandId, ownership.FactionId);
            }
        }

        var allMet = true;

        if (wzProto.Requirements != null)
        {
            foreach (var req in wzProto.Requirements)
            {
                if (!req.Check(attackerBand, attackerFaction, ownerships, frameTimeSec))
                {
                    allMet = false;
                    break;
                }
            }
        }

        if (!allMet)
            return;

        state.DefendingBandId = attackerBand;
        state.DefendingFactionId = attackerFaction;

        await _dbManager.SetStalkerZoneOwnershipAsync(
            wzComp.ZoneProto,
            attackerBand.HasValue ? new ProtoId<STBandPrototype>(attackerBand.Value.ToString()) : default,
            attackerFaction.HasValue ? new ProtoId<NpcFactionPrototype>(attackerFaction.Value.ToString()) : default);

        var msg = $"Zone '{wzComp.PortalName}' captured!";
        Logger.InfoS("warzone", msg);
    }

    private void ResetAllRequirements(EntityUid zone)
    {
        if (!_entityManager.TryGetComponent(zone, out WarZoneComponent? wzComp))
            return;

        if (!_prototypeManager.TryIndex<STWarZonePrototype>(wzComp.ZoneProto, out var wzProto))
            return;

        if (wzProto.Requirements == null)
            return;

        foreach (var req in wzProto.Requirements)
        {
            if (req is CaptureTimeRequirenment captureReq)
                captureReq.Reset();
        }
    }

    private void DistributeRewards(EntityUid zone, TimeSpan lastRewardTime, TimeSpan now)
    {
        if (!_entityManager.TryGetComponent(zone, out WarZoneComponent? wzComp))
            return;

        if (!_prototypeManager.TryIndex<STWarZonePrototype>(wzComp.ZoneProto, out var wzProto))
            return;

        var period = wzProto.RewardPeriod;

        if (now - lastRewardTime < period)
            return;

        if (!_activeCaptures.TryGetValue(zone, out var state))
            return;

        var points = wzProto.RewardPointsPerPeriod;

        if (state.DefendingBandId.HasValue)
        {
            _dbManager.SetStalkerBandAsync(
                new ProtoId<STBandPrototype>(state.DefendingBandId.Value.ToString()),
                points);
        }
        else if (state.DefendingFactionId.HasValue)
        {
            _dbManager.SetStalkerFactionAsync(
                new ProtoId<NpcFactionPrototype>(state.DefendingFactionId.Value.ToString()),
                points);
        }

        _lastRewardTimes[zone] = now;
    }

    private static int? GetFirst(HashSet<int> set)
    {
        foreach (var g in set)
            return g;
        return null;
    }

    private void OnStartCollide(EntityUid uid, WarZoneComponent component, ref StartCollideEvent args)
    {
        var other = args.OtherEntity;

        if (!_entityManager.TryGetComponent(other, out BandsComponent? bands))
            return;

        var bandId = bands.BandProto;
        ProtoId<NpcFactionPrototype>? factionId = null;

        if (_prototypeManager.TryIndex<STBandPrototype>(bandId, out var bandProto))
        {
            factionId = bandProto.FactionId;
        }

        if (!_activeCaptures.TryGetValue(uid, out var state))
        {
            state = new CaptureState();
            _activeCaptures[uid] = state;
        }

        state.PresentBandIds.Add(int.Parse(bandId));
        if (factionId != null)
            state.PresentFactionIds.Add(int.Parse(factionId));
    }

    private void OnEndCollide(EntityUid uid, WarZoneComponent component, ref EndCollideEvent args)
    {
        var other = args.OtherEntity;

        if (!_entityManager.TryGetComponent(other, out BandsComponent? bands))
            return;

        var bandId = bands.BandProto;
        ProtoId<NpcFactionPrototype>? factionId = null;

        if (_prototypeManager.TryIndex<STBandPrototype>(bandId, out var bandProto))
        {
            factionId = bandProto.FactionId;
        }

        if (!_activeCaptures.TryGetValue(uid, out var state))
            return;

        state.PresentBandIds.Remove(int.Parse(bandId));
        if (factionId != null)
            state.PresentFactionIds.Remove(int.Parse(factionId));
    }

    private sealed class CaptureState
    {
        public int? DefendingBandId;
        public int? DefendingFactionId;

        public HashSet<int> PresentBandIds = new();
        public HashSet<int> PresentFactionIds = new();
    }
}