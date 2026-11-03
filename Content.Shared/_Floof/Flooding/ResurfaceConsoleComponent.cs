using Robust.Shared.GameStates;

namespace Content.Shared._Floof.Flooding;

/// <summary>
///     A communications console whose evac controls say what they really do: bring the station back up.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ResurfaceConsoleComponent : Component;
