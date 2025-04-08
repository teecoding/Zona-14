using System;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Content.Server.Database;

namespace Content.Server._Stalker.WarZone.Requirenments;

[ImplicitDataDefinitionForInheritors]
[Serializable, NetSerializable]
public abstract partial class BaseWarZoneRequirenment
{
    public abstract bool Check(
        Guid? attackerBand,
        Guid? attackerFaction,
        Dictionary<ProtoId<STWarZonePrototype>, (Guid? BandId, Guid? FactionId)> ownerships,
        float frameTime);
}