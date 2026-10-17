using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Content.Server._Floof.Persistence.Migrations;
using Content.Server.Database;
using Content.Shared._Floof.Persistence;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Preferences;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Reflection;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Mapping;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Content.Server._Floof.Persistence;

/// <summary>
///     Loads, migrates, saves and discards live characters.
/// </summary>
public sealed class LiveCharacterManager
{
    [Dependency] private IServerDbManager _db = default!;
    [Dependency] private ISerializationManager _serialization = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IReflectionManager _reflection = default!;
    [Dependency] private IDynamicTypeFactory _typeFactory = default!;
    [Dependency] private ILogManager _log = default!;
    [Dependency] private MarkingManager _marking = default!;

    private Dictionary<int, ILiveCharacterMigration>? _migrations;

    private ISawmill Sawmill => _log.GetSawmill("live_character");

    /// <summary>
    ///     Migrations keyed by the version they upgrade from.
    /// </summary>
    public IReadOnlyDictionary<int, ILiveCharacterMigration> Migrations => _migrations ??= _reflection
        .GetAllChildren<ILiveCharacterMigration>()
        .Select(t => _typeFactory.CreateInstance<ILiveCharacterMigration>(t))
        .ToDictionary(m => m.FromVersion);

    /// <summary>
    ///     Loads the live character of a slot, migrating and saving it back if it is outdated.
    ///     Returns null if there is none or it can't be used, in which case the template should be used.
    /// </summary>
    public async Task<LiveCharacterRecord?> Load(NetUserId userId, int slot)
    {
        var live = await _db.GetLiveCharacterAsync(userId, slot);
        if (live == null)
            return null;

        LiveCharacterRecord record;
        try
        {
            var node = Parse(live.Data);
            if (!Migrate(node, live.SchemaVersion))
                return null;

            record = _serialization.Read<LiveCharacterRecord>(node, notNullableOverride: true);
        }
        catch (Exception e)
        {
            Sawmill.Error($"Failed to load live character {userId} slot {slot} (v{live.SchemaVersion}): {e}");
            return null;
        }

        if (live.SchemaVersion != LiveCharacterRecord.CurrentVersion)
            await Save(userId, slot, record, live.LastSavedRound, LiveCharacterHistoryReason.Migration);

        if (!_prototype.HasIndex<SpeciesPrototype>(record.Species))
        {
            Sawmill.Warning($"Live character {userId} slot {slot} has unknown species {record.Species}, using template");
            return null;
        }

        Validate(record, $"{userId} slot {slot}");
        return record;
    }

    /// <summary>
    ///     Drops data that no longer fits the current prototypes, the same way profiles are validated on load.
    /// </summary>
    public void Validate(LiveCharacterRecord record, string owner)
    {
        // Appearance validation indexes organ categories without checking, so drop unknown ones first.
        foreach (var organ in record.Appearance.Markings.Keys.ToList())
        {
            if (_prototype.HasIndex(organ))
                continue;

            Sawmill.Warning($"Live character {owner} has unknown organ category {organ}, dropping its markings");
            record.Appearance.Markings.Remove(organ);
        }

        record.Appearance = HumanoidCharacterAppearance.EnsureValid(record.Appearance, record.Species, record.Sex);

        // Organs outside the species body (genitals) skip appearance validation, so drop their unknown markings here.
        foreach (var layers in record.Appearance.Markings.Values)
        {
            foreach (var markings in layers.Values)
            {
                markings.RemoveAll(m => !_marking.TryGetMarking(m, out _));
            }
        }
    }

    public Task<bool> Save(
        NetUserId userId,
        int slot,
        LiveCharacterRecord record,
        int? roundId,
        LiveCharacterHistoryReason reason = LiveCharacterHistoryReason.RoundEnd)
    {
        return _db.SaveLiveCharacterAsync(userId, slot, LiveCharacterRecord.CurrentVersion, Serialize(record), roundId, reason);
    }

    public Task<bool> Discard(NetUserId userId, int slot)
    {
        return _db.DeleteLiveCharacterAsync(userId, slot);
    }

    /// <summary>
    ///     Builds the profile to spawn with: the live character if there is one, otherwise the template.
    /// </summary>
    public async Task<HumanoidCharacterProfile> GetSpawnProfile(NetUserId userId, int slot, HumanoidCharacterProfile template)
    {
        var record = await Load(userId, slot);
        return record?.ApplyTo(template) ?? template;
    }

    /// <summary>
    ///     Runs every migration from <paramref name="version"/> up to the current version, in order.
    /// </summary>
    public bool Migrate(MappingDataNode node, int version)
    {
        if (version > LiveCharacterRecord.CurrentVersion)
        {
            Sawmill.Error($"Live character version {version} is newer than {LiveCharacterRecord.CurrentVersion}");
            return false;
        }

        for (; version < LiveCharacterRecord.CurrentVersion; version++)
        {
            if (!Migrations.TryGetValue(version, out var migration))
            {
                Sawmill.Error($"No live character migration from version {version}");
                return false;
            }

            migration.Migrate(node);
        }

        return true;
    }

    public string Serialize(LiveCharacterRecord record)
    {
        var node = _serialization.WriteValueAs<MappingDataNode>(record, alwaysWrite: true);
        var stream = new YamlStream { new YamlDocument(node.ToYaml()) };
        var writer = new StringWriter();
        stream.Save(new YamlMappingFix(new Emitter(writer)), false);
        return writer.ToString();
    }

    public static MappingDataNode Parse(string yaml)
    {
        var stream = new YamlStream();
        stream.Load(new StringReader(yaml));
        return (MappingDataNode) stream.Documents[0].RootNode.ToDataNode();
    }
}
