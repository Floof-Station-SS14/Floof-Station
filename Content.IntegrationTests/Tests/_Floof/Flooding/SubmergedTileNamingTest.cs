using Content.Shared.Maps;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Floof.Flooding;

[TestFixture]
public sealed class SubmergedTileNamingTest
{
    /// <summary>
    /// Submerged tiles are endless liquid, so mappers must be able to tell them apart by ID and name.
    /// </summary>
    [Test]
    public async Task SubmergedTilesAreNamedTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var protoManager = server.ResolveDependency<IPrototypeManager>();
        var loc = server.ResolveDependency<ILocalizationManager>();

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                foreach (var tile in protoManager.EnumeratePrototypes<ContentTileDefinition>())
                {
                    var prefixed = tile.ID.StartsWith("Submerged");
                    Assert.That(tile.Submerged, Is.EqualTo(prefixed),
                        $"Tile {tile.ID}: submerged tiles and only those must have an ID starting with \"Submerged\".");

                    if (!tile.Submerged)
                        continue;

                    Assert.That(loc.GetString(tile.Name), Does.StartWith("submerged"),
                        $"Submerged tile {tile.ID} must have a name starting with \"submerged\".");
                    Assert.That(protoManager.HasIndex(tile.SubmergedReagent),
                        $"Submerged tile {tile.ID} has unknown reagent {tile.SubmergedReagent}.");
                }
            });
        });

        await pair.CleanReturnAsync();
    }
}
