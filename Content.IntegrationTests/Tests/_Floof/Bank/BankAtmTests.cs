using Content.IntegrationTests.Fixtures;
using Content.Server._Floof.Bank;
using Content.Server.GameTicking;
using Content.Shared._Floof.Bank;
using Content.Shared.GameTicking;
using Content.Shared.Stacks;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Floof.Bank;

[TestFixture]
public sealed class BankAtmTests : GameTest
{
    public override PoolSettings PoolSettings => new()
    {
        Dirty = true,
        DummyTicker = false,
        Connected = true,
        InLobby = true,
    };

    [Test]
    public async Task TestWithdrawInvalidAmounts()
    {
        var pair = Pair;
        var server = pair.Server;
        var entMan = server.EntMan;
        var ticker = server.System<ServerGameTicker>();
        var bank = server.System<BankSystem>();
        var user = pair.Client.User!.Value;

        await server.WaitPost(() =>
        {
            ticker.ToggleReadyAll(true);
            ticker.StartRound();
        });
        await pair.RunTicksSync(10);
        Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound));
        var player = pair.Player!.AttachedEntity!.Value;

        var atm = EntityUid.Invalid;
        await server.WaitPost(() => atm = entMan.SpawnEntity("Atm", entMan.GetComponent<TransformComponent>(player).Coordinates));

        int CashTotal()
        {
            var total = 0;
            var query = entMan.EntityQueryEnumerator<StackComponent>();
            while (query.MoveNext(out var stack))
            {
                if (stack.StackTypeId == "Credit")
                    total += stack.Count;
            }
            return total;
        }

        async Task Withdraw(int amount)
        {
            await server.WaitPost(() =>
                entMan.EventBus.RaiseLocalEvent(atm, new BankAtmWithdrawMessage(amount) { Actor = player, UiKey = BankAtmUiKey.Key }));
            await pair.RunTicksSync(10);
        }

        var balance = await bank.GetBalance(user, 0);
        Assert.That(balance, Is.Not.Null);
        var cash = 0;
        await server.WaitPost(() => cash = CashTotal());

        foreach (var amount in new[] { 0, -1, -1000, int.MinValue, balance!.Value + 1, int.MaxValue })
        {
            await Withdraw(amount);
            Assert.That(await bank.GetBalance(user, 0), Is.EqualTo(balance), $"withdrawing {amount} changed the balance");
            await server.WaitPost(() => Assert.That(CashTotal(), Is.EqualTo(cash), $"withdrawing {amount} spawned cash"));
        }

        await Withdraw(100);
        Assert.That(await bank.GetBalance(user, 0), Is.EqualTo(balance - 100));
        await server.WaitPost(() =>
        {
            Assert.That(CashTotal(), Is.EqualTo(cash + 100));
            Assert.That(entMan.GetComponent<BankAccountComponent>(player).Balance, Is.EqualTo(balance - 100));
        });
    }
}
