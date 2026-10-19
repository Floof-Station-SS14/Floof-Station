using Content.Shared.Stacks;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Floof.Bank;

/// <summary>
/// Withdraw-only ATM: pays out cash from the user's bank account. Deposits are deliberately not supported.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BankAtmComponent : Component
{
    [DataField]
    public ProtoId<StackPrototype> CashType = "Credit";

    [DataField]
    public SoundSpecifier ErrorSound = new SoundPathSpecifier("/Audio/Effects/Cargo/buzz_sigh.ogg");

    [DataField]
    public SoundSpecifier ConfirmSound = new SoundPathSpecifier("/Audio/Effects/Cargo/ping.ogg");
}

[Serializable, NetSerializable]
public enum BankAtmUiKey : byte
{
    Key,
}

/// <summary>
/// Balance of whoever is using the ATM, or null if they have no bank account.
/// </summary>
[Serializable, NetSerializable]
public sealed class BankAtmState(int? balance) : BoundUserInterfaceState
{
    public readonly int? Balance = balance;
}

[Serializable, NetSerializable]
public sealed class BankAtmWithdrawMessage(int amount) : BoundUserInterfaceMessage
{
    public readonly int Amount = amount;
}
