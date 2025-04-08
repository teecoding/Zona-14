using System;
using System.Collections.Generic;
using Content.Server._Stalker.WarZone.Requirenments;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Server._Stalker.WarZone;

[Prototype("stWarZone"), Serializable, NetSerializable]
public sealed class STWarZonePrototype : IPrototype
{
    [IdDataField, ViewVariables]
    public string ID { get; private set; } = default!;

    [DataField, ViewVariables]
    public float RewardPointsPerPeriod { get; set; } = default!;

    [DataField, ViewVariables]
    public TimeSpan RewardPeriod { get; set; } = default!;

    [DataField]
    public HashSet<BaseWarZoneRequirenment>? Requirements;
}