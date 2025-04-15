using System;
using Content.Shared.Mobs.Systems;
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;
using Content.Shared.Actions;
using Robust.Shared.Serialization;
using Robust.Shared.Network;

namespace Content.Shared._Stalker.Bands
{
    // UI key for bands managing UI
    [Serializable, NetSerializable]
    public enum BandsUiKey
    {
        Key = 0
    }
    [Virtual]
    public class SharedBandsSystem : EntitySystem
    {
        [Dependency] private readonly SharedActionsSystem _actions = default!;
        [Dependency] private readonly MobStateSystem _mobState = default!;
        [Dependency] private readonly IPrototypeManager _protoManager = default!;

        public override void Initialize()
        {
            base.Initialize();
        }

        // private void OnInit(EntityUid uid, BandsComponent component, ComponentInit args)
        // {
        //     EnsureComp<StatusIconComponent>(uid);

        //     if (component is { AltBand: not null, CanChange: true })
        //         _actions.AddAction(uid, ref component.ActionChangeEntity, component.ActionChange, uid);

        //     _actions.AddAction(uid, ref component.ActionEntity, component.Action, uid);
        // }
        // private void OnChange(Entity<BandsComponent> entity, ref ChangeBandEvent args)
        // {
        //     var comp = entity.Comp;
        //     if (comp.AltBand == null || !comp.CanChange)
        //         return;

        //     (comp.BandStatusIcon, comp.AltBand) = (comp.AltBand, comp.BandStatusIcon);
        //     Dirty(entity);
        //     args.Handled = true;
        // }

        // private void OnRemove(EntityUid uid, BandsComponent component, ComponentRemove args)
        // {
        //     RemComp<StatusIconComponent>(uid);

        //     var proto = _protoManager.Index<JobIconPrototype>(component.BandStatusIcon);

        //     _actions.RemoveAction(uid, component.ActionEntity);
        //     if (component.ActionChangeEntity != null)
        //         _actions.RemoveAction(uid, component.ActionChangeEntity);
        // }

        // private void OnToggle(EntityUid uid, BandsComponent component, ToggleBandsEvent args)
        // {
        //     if (!_mobState.IsAlive(uid))
        //         return;

        //     var proto = _protoManager.Index<JobIconPrototype>(component.BandStatusIcon);

        //     component.Enabled = !component.Enabled;
        //     Dirty(uid, component);

        //     args.Handled = true;
        // }
    }

    [Serializable, NetSerializable]
    public sealed class BandsManagingBoundUserInterfaceState : BoundUserInterfaceState
    {
        public string? BandName { get; }
        public int MaxMembers { get; }
        public List<BandMemberInfo> Members { get; }
        public bool CanManage { get; }
        public List<WarZoneInfo> WarZones { get; } // Added
        public List<BandPointsInfo> BandPoints { get; } // Added

        public BandsManagingBoundUserInterfaceState(
            string? bandName,
            int maxMembers,
            List<BandMemberInfo> members,
            bool canManage,
            List<WarZoneInfo>? warZones, // Added, nullable for safety during construction
            List<BandPointsInfo>? bandPoints) // Added, nullable for safety during construction
        {
            BandName = bandName;
            MaxMembers = maxMembers;
            Members = members; // Assuming members is never null based on existing code
            CanManage = canManage;
            // Initialize new lists - ensure they are never null
            WarZones = warZones ?? new List<WarZoneInfo>();
            BandPoints = bandPoints ?? new List<BandPointsInfo>();
        }
    }

    // --- New Data Structures for War Zone Tab ---

    [Serializable, NetSerializable]
    public sealed class WarZoneInfo
    {
        public string ZoneId { get; }
        public string Owner { get; }
        public float Cooldown { get; } // In seconds
        public string Attacker { get; }
        public string Defender { get; }
        public float Progress { get; } // 0.0 to 1.0

        public WarZoneInfo(string zoneId, string owner, float cooldown, string attacker, string defender, float progress)
        {
            ZoneId = zoneId;
            Owner = owner;
            Cooldown = cooldown;
            Attacker = attacker;
            Defender = defender;
            Progress = progress;
        }
    }

    [Serializable, NetSerializable]
    public sealed class BandPointsInfo
    {
        public string BandProtoId { get; }
        public string BandName { get; }
        public float Points { get; }

        public BandPointsInfo(string bandProtoId, string bandName, float points)
        {
            BandProtoId = bandProtoId;
            BandName = bandName;
            Points = points;
        }
    }

    // --- Existing Data Structures ---

    [Serializable, NetSerializable]
    public sealed class BandMemberInfo
    {
        public NetUserId UserId { get; }
        public string PlayerName { get; }
        public string RoleId { get; }

        public BandMemberInfo(NetUserId userId, string playerName, string roleId)
        {
            UserId = userId;
            PlayerName = playerName;
            RoleId = roleId;
        }
    }

    [Serializable, NetSerializable]
    public sealed class BandsManagingAddMemberMessage : BoundUserInterfaceMessage
    {
        public string PlayerName { get; }

        public BandsManagingAddMemberMessage(string playerName)
        {
            PlayerName = playerName;
        }
    }

    [Serializable, NetSerializable]
    public sealed class BandsManagingRemoveMemberMessage : BoundUserInterfaceMessage
    {
        public Guid PlayerUserId { get; }

        public BandsManagingRemoveMemberMessage(Guid playerUserId)
        {
            PlayerUserId = playerUserId;
        }
    }
}