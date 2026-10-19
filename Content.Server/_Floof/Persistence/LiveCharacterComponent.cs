using Robust.Shared.Network;

namespace Content.Server._Floof.Persistence;

/// <summary>
///     Marks a body spawned from a player's character slot, so it can be saved as that slot's live character.
/// </summary>
[RegisterComponent]
public sealed partial class LiveCharacterComponent : Component
{
    [ViewVariables]
    public NetUserId User;

    [ViewVariables]
    public int Slot;
}
