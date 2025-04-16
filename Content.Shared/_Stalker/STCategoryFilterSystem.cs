using Robust.Shared;
using Robust.Shared.Configuration;

namespace Content.Shared._Stalker;

public sealed class STCategoryFilterSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    public override void Initialize()
    {
        _cfg.SetCVar(CVars.EntitiesCategoryFilter, "ForkFiltered");
    }
}
