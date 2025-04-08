using System;
using System.Collections.Generic;
using Content.Server._Stalker.WarZone;
using Content.Shared._Stalker.WarZone;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Stalker.WarZone;

/// <summary>
/// Handles War Zone capture logic, contestation, ownership persistence, and reward distribution.
/// </summary>
public sealed partial class WarZoneSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IServerDbManager _dbManager = default!;

    /// <summary>
    /// Tracks ongoing capture states per War Zone entity.
    /// </summary>
    private readonly Dictionary<EntityUid, CaptureState> _activeCaptures = new();

    /// <summary>
    /// Tracks last reward distribution timestamps per War Zone.
    /// </summary>
    private readonly Dictionary<EntityUid, TimeSpan> _lastRewardTimes = new();

    public override void Initialize()
    {
        base.Initialize();

        // Subscribe to trigger events for player entry/exit
        SubscribeLocalEvent<WarZoneComponent, StartCollideEvent>(OnStartCollide);
        SubscribeLocalEvent<WarZoneComponent, EndCollideEvent>(OnEndCollide);

        // TODO: Load initial ownership states from database
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _gameTiming.CurTime;

        foreach (var (zone, state) in _activeCaptures)
        {
            UpdateCapture(zone, state, now);
        }

        foreach (var (zone, lastRewardTime) in _lastRewardTimes)
        {
            DistributeRewards(zone, lastRewardTime, now);
        }
    }

    private void UpdateCapture(EntityUid zone, CaptureState state, TimeSpan now)
    {
        // Pause/reset if multiple groups present
        if (state.PresentBandIds.Count > 1 || state.PresentFactionIds.Count > 1)
        {
            state.ProgressSeconds = 0;
            return;
        }

        // Pause/reset if no one present
        if (state.PresentBandIds.Count == 0 && state.PresentFactionIds.Count == 0)
        {
            state.ProgressSeconds = 0;
            return;
        }

        // Determine attacker
        Guid? attackerBand = null;
        Guid? attackerFaction = null;

        if (state.PresentBandIds.Count == 1)
            attackerBand = GetFirst(state.PresentBandIds);
        if (state.PresentFactionIds.Count == 1)
            attackerFaction = GetFirst(state.PresentFactionIds);

        // If attacker is current owner, do nothing
        if ((attackerBand != null && attackerBand == state.DefendingBandId) ||
            (attackerFaction != null && attackerFaction == state.DefendingFactionId))
        {
            state.ProgressSeconds = 0;
            return;
        }

        // Increment timer
        state.ProgressSeconds += (float)_gameTiming.FrameTime.TotalSeconds;

        // Fetch capture time requirement from prototype
        float captureTime = 30f; // default fallback

        if (_entityManager.TryGetComponent(zone, out WarZoneComponent? wzComp) &&
            _prototypeManager.TryIndex<STWarZonePrototype>(wzComp.ZoneProto, out var wzProto))
        {
            // TODO: Replace with wzProto.CaptureTimeRequirement when added
            captureTime = (float)wzProto.RewardPeriod.TotalSeconds; // temporary use of RewardPeriod
        }

        if (state.ProgressSeconds >= captureTime)
        {
            // Transfer ownership
            state.DefendingBandId = attackerBand;
            state.DefendingFactionId = attackerFaction;
            state.ProgressSeconds = 0;

            // Update database ownership
            _dbManager.SetStalkerZoneOwnershipAsync(
                wzComp.ZoneProto,
                attackerBand != null ? new ProtoId<STBandPrototype>(attackerBand.ToString()) : null,
                attackerFaction != null ? new ProtoId<NpcFactionPrototype>(attackerFaction.ToString()) : null);

            // Announce capture success
            var msg = $"Zone '{wzComp.PortalName}' captured!";
            // TODO: Replace with proper chat or popup system
            Logger.InfoS("warzone", msg);
        }
    }

    private static Guid GetFirst(HashSet<Guid> set)
    {
        foreach (var g in set)
            return g;
        return Guid.Empty;
    }

    private void DistributeRewards(EntityUid zone, TimeSpan lastRewardTime, TimeSpan now)
    {
        // TODO: Check if RewardPeriod elapsed
        // If so, add RewardPointsPerPeriod to owner and update timestamp
    }

    private sealed class CaptureState
    {
        public TimeSpan CaptureStartTime;
        public float ProgressSeconds;
        public Guid? AttackingBandId;
        public Guid? AttackingFactionId;
        public Guid? DefendingBandId;
        public Guid? DefendingFactionId;

        public HashSet<Guid> PresentBandIds = new();
        public HashSet<Guid> PresentFactionIds = new();
    }
    private void OnStartCollide(EntityUid uid, WarZoneComponent component, ref StartCollideEvent args)
    {
        var other = args.OtherEntity;

        if (!_entityManager.TryGetComponent(other, out BandsComponent? bands))
            return; // Not a player or no band info

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

        // TODO: Implement logic to start or continue capture based on presence
    }

    private void OnEndCollide(EntityUid uid, WarZoneComponent component, ref EndCollideEvent args)
    {
        var other = args.OtherEntity;

        if (!_entityManager.TryGetComponent(other, out BandsComponent? bands))
            return; // Not a player or no band info

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

        // TODO: Implement logic to pause or reset capture if no allies remain
    }
}