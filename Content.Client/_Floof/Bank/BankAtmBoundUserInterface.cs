using Content.Shared._Floof.Bank;
using Robust.Client.UserInterface;

namespace Content.Client._Floof.Bank;

public sealed class BankAtmBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private BankAtmWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<BankAtmWindow>();
        _window.OnWithdraw += amount => SendMessage(new BankAtmWithdrawMessage(amount));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is BankAtmState atm)
            _window?.UpdateState(atm.Balance);
    }
}
