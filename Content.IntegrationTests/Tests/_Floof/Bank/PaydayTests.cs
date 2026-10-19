using Content.IntegrationTests.Fixtures;
using Content.Server._Floof.Bank;
using Content.Server.GameTicking;
using Content.Shared._Floof.CCVars;
using Content.Shared.GameTicking;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Configuration;

namespace Content.IntegrationTests.Tests._Floof.Bank;

[TestFixture]
public sealed class PaydayTests : GameTest
{
    public override PoolSettings PoolSettings => new()
    {
        Dirty = true,
        DummyTicker = false,
        Connected = true,
        InLobby = true,
    };

    [Test]
    public async Task TestPayday()
    {
        var pair = Pair;
        var server = pair.Server;
        var ticker = server.System<ServerGameTicker>();
        var bank = server.System<BankSystem>();
        var mobState = server.System<MobStateSystem>();
        var cfg = server.ResolveDependency<IConfigurationManager>();
        var user = pair.Client.User!.Value;
        var start = cfg.GetCVar(CCVars.BankStartingBalance);

        await server.WaitPost(() =>
        {
            cfg.SetCVar(CCVars.BankAliveCheckInterval, 1f);
            cfg.SetCVar(CCVars.BankChecksPerPayday, 2);
            ticker.ToggleReadyAll(true);
            ticker.StartRound();
        });
        await pair.RunTicksSync(10);
        Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound));
        var player = pair.Player!.AttachedEntity!.Value;

        // Alive for the whole shift: full pay.
        await pair.RunSeconds(2.5f);
        var balance = await bank.GetBalance(user, 0);
        Assert.That(balance, Is.GreaterThan(start));

        // Dead for the whole shift: nothing.
        await server.WaitPost(() => mobState.ChangeMobState(player, MobState.Dead));
        await pair.RunSeconds(2f);
        Assert.That(await bank.GetBalance(user, 0), Is.EqualTo(balance));

        // Alive again, but the round has ended: no more paydays.
        await server.WaitPost(() =>
        {
            mobState.ChangeMobState(player, MobState.Alive);
            ticker.EndRound();
        });
        await pair.RunSeconds(2.5f);
        Assert.That(await bank.GetBalance(user, 0), Is.EqualTo(balance));
    }
}
