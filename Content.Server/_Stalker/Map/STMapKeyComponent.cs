using Robust.Shared.GameStates;

namespace Content.Server._Stalker.Map;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class STMapKeyComponent : Component
{
    [DataField, AutoNetworkedField]
    public string Value;
}
