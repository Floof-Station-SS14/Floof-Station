using Content.Client.IconSmoothing;
using Content.Client.IconSmoothing.Smoothings;
using Content.Shared._Floof.Flooding;
using Content.Shared.Maps;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Client._Floof.Flooding;

/// <summary>
///     <see cref="DirectionSpriteSmoothing"/>, except that under the sea a puddle also connects to the exposed tiles
///     around it. The sea has no puddles of its own to connect to, and the water shouldn't round off where it meets them.
/// </summary>
public sealed partial class FloodPuddleSmoothing : ISpriteSmoothState
{
    [DataField(required: true)]
    public string Base { get; set; } = default!;

    [DataField(required: true)]
    public HashSet<string> Mask { get; set; } = default!;

    [DataField]
    public string LayerKey { get; set; } = "direction";

    [DataField]
    public int? Index { get; set; }

    [DataField]
    public ProtoId<ShaderPrototype>? Shader { get; set; }

    public void InitializeStates(Entity<SpriteComponent> entity, SpriteSystem sprite)
    {
        sprite.LayerMapSet(entity.AsNullable(), LayerKey, sprite.AddRsiLayer(entity.AsNullable(), Base + 0, index: Index));
    }

    public IEnumerable<SpriteLayer> EnumerateStates(HashSet<string>?[] layers, Entity<SpriteComponent> entity, SpriteSystem sprite)
    {
        var match = DirectionFlag.None;
        for (byte i = 0; 2 * i < IconSmoothSystem.Directions; i++)
        {
            if (layers[2 * i] is { } keys && keys.Overlaps(Mask) || IsSea(entity, (Direction) (2 * i)))
                match |= (DirectionFlag) (1 << i);
        }

        yield return new SpriteLayer(LayerKey, Base + (byte) match);
    }

    private static bool IsSea(EntityUid uid, Direction direction)
    {
        var entMan = IoCManager.Resolve<IEntityManager>();
        var xform = entMan.GetComponent<TransformComponent>(uid);
        if (!entMan.HasComponent<FloodAmbientPressureComponent>(xform.MapUid) ||
            !entMan.TryGetComponent<MapGridComponent>(xform.GridUid, out var grid))
            return false;

        var map = entMan.System<SharedMapSystem>();
        var neighbor = map.TileIndicesFor(xform.GridUid.Value, grid, xform.Coordinates).Offset(direction);
        return !map.TryGetTileRef(xform.GridUid.Value, grid, neighbor, out var tile) ||
               tile.Tile.IsEmpty ||
               entMan.System<TurfSystem>().IsSpace(tile);
    }
}
