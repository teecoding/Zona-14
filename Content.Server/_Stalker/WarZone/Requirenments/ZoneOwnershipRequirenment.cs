using System;
using System.Collections.Generic;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Content.Server.Database;

namespace Content.Server._Stalker.WarZone.Requirenments;

[Serializable, NetSerializable]
public sealed partial class ZoneOwnershipRequirenment : BaseWarZoneRequirenment
{
    [DataField("requiredZones")]
    public List<ProtoId<STWarZonePrototype>> RequiredZones = new();

    public override bool Check(
        Guid? attackerBand,
        Guid? attackerFaction,
        Dictionary<ProtoId<STWarZonePrototype>, (Guid? BandId, Guid? FactionId)> ownerships,
        float frameTime)
    {
        foreach (var zoneId in RequiredZones)
        {
            if (!ownerships.TryGetValue(zoneId, out var owner))
                return false;

            var owns = false;
            if (attackerBand != null && owner.BandId == attackerBand)
                owns = true;
            if (attackerFaction != null && owner.FactionId == attackerFaction)
                owns = true;

            if (!owns)
                return false;
        }

        return true;
    }
}