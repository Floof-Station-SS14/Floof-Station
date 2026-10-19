using Content.Server.GameTicking;
using Content.Server.Shuttles.Systems;
using Content.Shared._Floof.CCVars;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Roles;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Floof.Bank;

/// <summary>
/// Pays every crewmember at the same time each payday. Alive checks run in between;
/// checks spent dead (or not spawned yet) reduce the pay to half or nothing.
/// </summary>
public sealed partial class PaydaySystem : EntitySystem
{
    [Dependency] private BankSystem _bank = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private EmergencyShuttleSystem _emergency = default!;
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    private readonly Dictionary<NetUserId, PaydayRecord> _records = new();
    private TimeSpan? _nextCheck;
    private int _checks;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawn);
        SubscribeLocalEvent<GameRunLevelChangedEvent>(OnRunLevelChanged);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    /// <summary>
    /// Full pay if dead for less than half of the checks, nothing if dead for all of them, half otherwise.
    /// </summary>
    public static int CalculatePay(int pay, int aliveChecks, int checks)
    {
        if ((checks - aliveChecks) * 2 < checks)
            return pay;

        return aliveChecks > 0 ? pay / 2 : 0;
    }

    public override void Update(float frameTime)
    {
        if (_ticker.RunLevel != GameRunLevel.InRound)
            return;

        var interval = TimeSpan.FromSeconds(_cfg.GetCVar(CCVars.BankAliveCheckInterval));
        _nextCheck ??= _timing.CurTime + interval;
        if (_timing.CurTime < _nextCheck)
            return;

        _nextCheck += interval;
        _checks++;

        foreach (var record in _records.Values)
        {
            if (IsAlive(record))
                record.AliveChecks++;
        }

        if (_checks < _cfg.GetCVar(CCVars.BankChecksPerPayday))
            return;

        foreach (var (userId, record) in _records)
        {
            var pay = GetPay(record);
            var amount = CalculatePay(pay, record.AliveChecks, _checks);
            var message = amount == pay ? "bank-payday" : "bank-payday-reduced";
            record.AliveChecks = 0;
            Pay(userId, record, amount, message);
        }

        _checks = 0;
    }

    private void OnPlayerSpawn(PlayerSpawnCompleteEvent ev)
    {
        if (!_bank.TryGetSlot(ev, out var slot)
            || !_mind.TryGetMind(ev.Mob, out var mindId, out _))
            return;

        _records[ev.Player.UserId] = new PaydayRecord(slot, mindId, ev.JobId);
    }

    private void OnRunLevelChanged(GameRunLevelChangedEvent ev)
    {
        if (ev.New != GameRunLevel.PostRound)
            return;

        foreach (var (userId, record) in _records)
        {
            if (TryComp<MindComponent>(record.Mind, out var mind)
                && mind.OwnedEntity is { } body
                && _emergency.IsTargetEscaping(body))
                Pay(userId, record, GetPay(record), "bank-cc-bonus");
        }
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _records.Clear();
        _nextCheck = null;
        _checks = 0;
    }

    private bool IsAlive(PaydayRecord record)
    {
        return TryComp<MindComponent>(record.Mind, out var mind)
            && mind.OwnedEntity is { } body
            && !_mobState.IsDead(body);
    }

    private int GetPay(PaydayRecord record)
    {
        return record.Job is { } job && _proto.TryIndex<JobPrototype>(job, out var proto) ? proto.Pay : 0;
    }

    private async void Pay(NetUserId userId, PaydayRecord record, int amount, string message)
    {
        if (amount <= 0)
            return;

        var balance = await _bank.Deposit(userId, record.Slot, amount, message);
        if (balance == null
            || !_player.TryGetSessionById(userId, out var session)
            || session.AttachedEntity is not { } attached)
            return;

        _popup.PopupEntity(Loc.GetString(message, ("amount", amount), ("balance", balance.Value)), attached, session);
    }

    private sealed class PaydayRecord(int slot, EntityUid mind, string? job)
    {
        public readonly int Slot = slot;
        public readonly EntityUid Mind = mind;
        public readonly string? Job = job;
        public int AliveChecks;
    }
}
