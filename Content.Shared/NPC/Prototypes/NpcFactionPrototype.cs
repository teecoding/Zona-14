using Robust.Shared.Prototypes;

namespace Content.Shared.NPC.Prototypes;

/// <summary>
/// Contains data about this faction's relations with other factions.
/// </summary>
[Prototype("npcFaction")]
public sealed partial class NpcFactionPrototype : IPrototype
{
    [ViewVariables]
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public List<ProtoId<NpcFactionPrototype>> Friendly = new();
    [DataField]
    public List<ProtoId<NpcFactionPrototype>> Hostile = new();

    /// <summary>
    /// The database ID for this faction. This is used to match the faction with its database record.
    /// </summary>
    [DataField]
    public int DatabaseId { get; set; } = 0;
}

/// <summary>
/// Cached data for the faction prototype. Is modified at runtime, whereas the prototype is not.
/// </summary>
public record struct FactionData
{
    [ViewVariables]
    public HashSet<ProtoId<NpcFactionPrototype>> Friendly;

    [ViewVariables]
    public HashSet<ProtoId<NpcFactionPrototype>> Hostile;
}
