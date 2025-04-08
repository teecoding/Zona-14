using System;
using System.Collections.Generic;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Content.Server.Database;

namespace Content.Server._Stalker.WarZone.Requirenments;

[Serializable, NetSerializable]
public sealed partial class CaptureTimeRequirenment : BaseWarZoneRequirenment
{
    [DataField("captureTime")]
    public float CaptureTime = 30f;

    [NonSerialized]
    public float ProgressSeconds = 0f;

    public override bool Check(
        Guid? attackerBand,
        Guid? attackerFaction,
        Dictionary<ProtoId<STWarZonePrototype>, (Guid? BandId, Guid? FactionId)> ownerships,
        float frameTime)
    {
        ProgressSeconds += frameTime;
        return ProgressSeconds >= CaptureTime;
    }

    public void Reset()
    {
        ProgressSeconds = 0f;
    }
}