using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;

namespace Content.Server._Floof.Flooding;

/// <summary>
///     Puddle on a submerged tile. Topped up to a full tile of <see cref="Reagent"/> every spread, so it never runs dry.
/// </summary>
[RegisterComponent, Access(typeof(SubmergedTileSystem))]
public sealed partial class SubmergedPuddleComponent : Component
{
    [DataField]
    public ProtoId<ReagentPrototype> Reagent = "Water";
}
