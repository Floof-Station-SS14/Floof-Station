using Robust.Shared.GameStates;
using Robust.Shared.Network;

namespace Content.Shared._Floof.Bank;

/// <summary>
/// Links a mob to its character's bank account and caches the balance for UIs. Only the owner gets the state.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BankAccountComponent : Component
{
    public override bool SendOnlyToOwner => true;

    [DataField, AutoNetworkedField]
    public int Balance;

    [ViewVariables]
    public NetUserId UserId;

    [ViewVariables]
    public int Slot;
}
