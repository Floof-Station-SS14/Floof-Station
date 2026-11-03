using Robust.Shared.GameStates;

namespace Content.Shared._Floof.Flooding;

/// <summary>
///     Outer clothing too heavy to swim in: the wearer goes under at swimming depth and strokes at reduced speed.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class FloodSinkingComponent : Component;
