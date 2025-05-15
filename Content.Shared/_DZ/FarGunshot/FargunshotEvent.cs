using Robust.Shared.Serialization;

namespace Content.Shared._DZ.FarGunshot;

[NetSerializable]
public sealed class FargunshotEvent : EntityEventArgs
{
    public EntityUid GunUid { get; set; }

    public FargunshotEvent(EntityUid gunUid)
    {
        GunUid = gunUid;
    }
    private FargunshotEvent() {}
}

