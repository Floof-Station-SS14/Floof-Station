using System.Threading.Tasks;
using Content.Server.Administration.Logs;
using Content.Server.Database;
using Content.Server.Preferences.Managers;
using Content.Shared._Floof.Bank;
using Content.Shared._Floof.CCVars;
using Content.Shared.Database;
using Content.Shared.GameTicking;
using Robust.Shared.Configuration;
using Robust.Shared.Network;

namespace Content.Server._Floof.Bank;

/// <summary>
/// Personal bank accounts, one per character slot. The balance lives in its own table so profile saves never touch it.
/// Spawned characters get a <see cref="BankAccountComponent"/> caching their balance.
/// </summary>
public sealed partial class BankSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IServerDbManager _db = default!;
    [Dependency] private IServerPreferencesManager _prefs = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawn);
    }

    /// <summary>
    /// The character slot a player spawned as, if it's one of their saved characters.
    /// </summary>
    public bool TryGetSlot(PlayerSpawnCompleteEvent ev, out int slot)
    {
        slot = 0;
        return _prefs.GetPreferencesOrNull(ev.Player.UserId) is { } prefs
            && prefs.TryIndexOfCharacter(ev.Profile, out slot);
    }

    private async void OnPlayerSpawn(PlayerSpawnCompleteEvent ev)
    {
        if (!TryGetSlot(ev, out var slot))
            return;

        var mob = ev.Mob;
        var account = EnsureComp<BankAccountComponent>(mob);
        account.UserId = ev.Player.UserId;
        account.Slot = slot;

        var balance = await GetBalance(account.UserId, slot);
        if (balance != null && !TerminatingOrDeleted(mob))
            UpdateAccounts(account.UserId, slot, balance.Value);
    }

    public Task<int?> GetBalance(NetUserId userId, int slot)
    {
        return _db.GetBankBalanceAsync(userId, slot, _cfg.GetCVar(CCVars.BankStartingBalance));
    }

    /// <summary>
    /// Adds money to an account. Returns the new balance, or null if the slot doesn't exist.
    /// </summary>
    public async Task<int?> Deposit(NetUserId userId, int slot, int amount, string reason)
    {
        var balance = await _db.AddBankBalanceAsync(userId, slot, amount, _cfg.GetCVar(CCVars.BankStartingBalance));
        if (balance == null)
            return null;

        _adminLog.Add(LogType.Bank, LogImpact.Low, $"{userId} slot {slot} received {amount} ({reason}), balance {balance.Value}");
        UpdateAccounts(userId, slot, balance.Value);
        return balance;
    }

    /// <summary>
    /// Takes money from an account. Returns the new balance, or null if there isn't enough.
    /// </summary>
    public async Task<int?> Withdraw(Entity<BankAccountComponent> ent, int amount)
    {
        var (userId, slot) = (ent.Comp.UserId, ent.Comp.Slot);
        var balance = await _db.AddBankBalanceAsync(userId, slot, -amount, _cfg.GetCVar(CCVars.BankStartingBalance));
        if (balance == null)
            return null;

        _adminLog.Add(LogType.Bank, LogImpact.Low, $"{userId} slot {slot} withdrew {amount}, balance {balance.Value}");
        UpdateAccounts(userId, slot, balance.Value);
        return balance;
    }

    private void UpdateAccounts(NetUserId userId, int slot, int balance)
    {
        var query = EntityQueryEnumerator<BankAccountComponent>();
        while (query.MoveNext(out var uid, out var account))
        {
            if (account.UserId != userId || account.Slot != slot)
                continue;

            account.Balance = balance;
            Dirty(uid, account);
        }
    }
}
