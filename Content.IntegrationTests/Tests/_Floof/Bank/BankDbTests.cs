using Content.IntegrationTests.Fixtures;
using Content.Server._Floof.Bank;
using Content.Server.Database;
using Content.Shared.Preferences;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Robust.Shared.Configuration;
using Robust.Shared.Log;
using Robust.Shared.Network;
using Robust.Shared.Serialization.Manager;
using Robust.UnitTesting;

namespace Content.IntegrationTests.Tests._Floof.Bank;

[TestFixture]
public sealed class BankDbTests : GameTest
{
    private const int Start = 5000;

    private static ServerDbSqlite GetDb(RobustIntegrationTest.ServerIntegrationInstance server)
    {
        var cfg = server.ResolveDependency<IConfigurationManager>();
        var serialization = server.ResolveDependency<ISerializationManager>();
        var opsLog = server.ResolveDependency<ILogManager>().GetSawmill("db.ops");
        var builder = new DbContextOptionsBuilder<SqliteServerDbContext>();
        var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        builder.UseSqlite(conn);
        return new ServerDbSqlite(() => builder.Options, true, cfg, true, opsLog, serialization, false);
    }

    private static NetUserId NewUserId() => new(Guid.NewGuid());

    [Test]
    public async Task TestBalancePerSlot()
    {
        var db = GetDb(Pair.Server);
        var user = NewUserId();

        Assert.That(await db.GetBankBalanceAsync(user, 0, Start), Is.Null, "no profile, no account");

        await db.InitPrefsAsync(user, new HumanoidCharacterProfile());
        await db.SaveCharacterSlotAsync(user, new HumanoidCharacterProfile(), 1);

        Assert.That(await db.GetBankBalanceAsync(user, 0, Start), Is.EqualTo(Start));
        Assert.That(await db.AddBankBalanceAsync(user, 0, 1000, Start), Is.EqualTo(Start + 1000));
        Assert.That(await db.AddBankBalanceAsync(user, 0, 500, Start), Is.EqualTo(Start + 1500));
        Assert.That(await db.GetBankBalanceAsync(user, 1, Start), Is.EqualTo(Start), "slots have separate accounts");
        Assert.That(await db.AddBankBalanceAsync(user, 2, 1000, Start), Is.Null, "missing slot");
        Assert.That(await db.AddBankBalanceAsync(user, 0, -(Start + 1500), Start), Is.EqualTo(0), "withdraw everything");
        Assert.That(await db.AddBankBalanceAsync(user, 0, -1, Start), Is.Null, "can't overdraw");
        Assert.That(await db.GetBankBalanceAsync(user, 0, Start), Is.EqualTo(0));
    }

    [Test]
    public async Task TestProfileSaveKeepsBalance()
    {
        var db = GetDb(Pair.Server);
        var user = NewUserId();
        await db.InitPrefsAsync(user, new HumanoidCharacterProfile());
        await db.AddBankBalanceAsync(user, 0, 1000, Start);

        await db.SaveCharacterSlotAsync(user, new HumanoidCharacterProfile().WithAge(40), 0);

        Assert.That(await db.GetBankBalanceAsync(user, 0, Start), Is.EqualTo(Start + 1000));
    }

    [Test]
    public async Task TestDeleteSlotDeletesAccount()
    {
        var db = GetDb(Pair.Server);
        var user = NewUserId();
        await db.InitPrefsAsync(user, new HumanoidCharacterProfile());
        await db.SaveCharacterSlotAsync(user, new HumanoidCharacterProfile(), 1);
        await db.AddBankBalanceAsync(user, 1, 1000, Start);

        await db.SaveCharacterSlotAsync(user, null, 1);
        Assert.That(await db.GetBankBalanceAsync(user, 1, Start), Is.Null);

        await db.SaveCharacterSlotAsync(user, new HumanoidCharacterProfile(), 1);
        Assert.That(await db.GetBankBalanceAsync(user, 1, Start), Is.EqualTo(Start), "new character starts fresh");
    }

    [TestCase(6, 6, 1000)]
    [TestCase(4, 6, 1000)]
    [TestCase(3, 6, 500)]
    [TestCase(1, 6, 500)]
    [TestCase(0, 6, 0)]
    public void TestCalculatePay(int alive, int checks, int expected)
    {
        Assert.That(PaydaySystem.CalculatePay(1000, alive, checks), Is.EqualTo(expected));
    }
}
