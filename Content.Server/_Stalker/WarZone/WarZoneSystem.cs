using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Content.Server._Stalker.WarZone;
using Content.Server._Stalker.WarZone.Requirenments;
using Content.Server.Database;
using Content.Shared._Stalker.WarZone;
using Content.Shared.Physics;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Physics.Events;

using Content.Shared.Physics;

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

        Guid? attackerBand = null;
        Guid? attackerFaction = null;

        if (state.PresentBandIds.Count == 1)
            attackerBand = GetFirst(state.PresentBandIds);
        if (state.PresentFactionIds.Count == 1)
            attackerFaction = GetFirst(state.PresentFactionIds);

        if ((attackerBand != null && attackerBand == state.DefendingBandId) ||
            (attackerFaction != null && attackerFaction == state.DefendingFactionId))
        {
            ResetAllRequirements(zone);
            return;
        }

        if (!_entityManager.TryGetComponent(zone, out WarZoneComponent? wzComp) ||
            !_prototypeManager.TryIndex<STWarZonePrototype>(wzComp.ZoneProto, out var wzProto))
            return;

        var ownerships = new Dictionary<ProtoId<STWarZonePrototype>, (Guid? BandId, Guid? FactionId)>();

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
            attackerBand != null ? new ProtoId<STBandPrototype>(attackerBand.ToString()) : null,
            attackerFaction != null ? new ProtoId<NpcFactionPrototype>(attackerFaction.ToString()) : null);

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

        if (state.DefendingBandId != null)
        {
            _dbManager.SetStalkerBandAsync(
                new ProtoId<STBandPrototype>(state.DefendingBandId.ToString()),
                points);
        }
        else if (state.DefendingFactionId != null)
        {
            _dbManager.SetStalkerFactionAsync(
                new ProtoId<NpcFactionPrototype>(state.DefendingFactionId.ToString()),
                points);
        }

        _lastRewardTimes[zone] = now;
    }

    private static Guid GetFirst(HashSet<Guid> set)
    {
        foreach (var g in set)
            return g;
        return Guid.Empty;
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

        state.PresentBandIds.Add(Guid.Parse(bandId));
        if (factionId != null)
            state.PresentFactionIds.Add(Guid.Parse(factionId));
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

        state.PresentBandIds.Remove(Guid.Parse(bandId));
        if (factionId != null)
            state.PresentFactionIds.Remove(Guid.Parse(factionId));
    }

    private sealed class CaptureState
    {
        public Guid? DefendingBandId;
        public Guid? DefendingFactionId;

        public HashSet<Guid> PresentBandIds = new();
        public HashSet<Guid> PresentFactionIds = new();
    }
}