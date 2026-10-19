using Content.Server.Stack;
using Content.Shared._Floof.Bank;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;

namespace Content.Server._Floof.Bank;

public sealed partial class BankAtmSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private BankSystem _bank = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private StackSystem _stack = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BankAtmComponent, BoundUIOpenedEvent>(OnOpened);
        SubscribeLocalEvent<BankAtmComponent, BankAtmWithdrawMessage>(OnWithdraw);
    }

    private void OnOpened(Entity<BankAtmComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent, args.Actor);
    }

    private void OnWithdraw(Entity<BankAtmComponent> ent, ref BankAtmWithdrawMessage args)
    {
        Withdraw(ent, args.Actor, args.Amount);
    }

    private async void Withdraw(Entity<BankAtmComponent> ent, EntityUid user, int amount)
    {
        if (!TryComp<BankAccountComponent>(user, out var account))
        {
            Deny(ent, user, "bank-atm-no-account");
            return;
        }

        if (amount <= 0 || amount > account.Balance)
        {
            Deny(ent, user, "bank-atm-insufficient-funds");
            return;
        }

        var balance = await _bank.Withdraw((user, account), amount);
        if (TerminatingOrDeleted(ent) || TerminatingOrDeleted(user))
            return;

        if (balance == null)
        {
            Deny(ent, user, "bank-atm-insufficient-funds");
            return;
        }

        foreach (var cash in _stack.SpawnMultipleAtPosition(ent.Comp.CashType, amount, Transform(user).Coordinates))
        {
            _hands.PickupOrDrop(user, cash);
        }

        _audio.PlayPvs(ent.Comp.ConfirmSound, ent);
        _popup.PopupEntity(Loc.GetString("bank-atm-withdrawn", ("amount", amount)), user, user);
        UpdateUi(ent, user);
    }

    private void Deny(Entity<BankAtmComponent> ent, EntityUid user, string message)
    {
        _audio.PlayPvs(ent.Comp.ErrorSound, ent);
        _popup.PopupEntity(Loc.GetString(message), user, user);
        UpdateUi(ent, user);
    }

    private void UpdateUi(Entity<BankAtmComponent> ent, EntityUid user)
    {
        var balance = TryComp<BankAccountComponent>(user, out var account) ? account.Balance : (int?) null;
        _ui.SetUiState(ent.Owner, BankAtmUiKey.Key, new BankAtmState(balance));
    }
}
