using Content.Server.Fluids.EntitySystems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Maps;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using static Content.Shared._Floof.Flooding.FloodConstants;

namespace Content.Server._Floof.Flooding;

/// <summary>
///     Keeps a source puddle on every submerged tile.
/// </summary>
public sealed partial class SubmergedTileSystem : EntitySystem
{
    [Dependency] private PuddleSystem _puddle = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private TurfSystem _turf = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TileChangedEvent>(OnTileChanged);
        SubscribeLocalEvent<MapGridComponent, MapInitEvent>(OnGridMapInit);
    }

    private void OnGridMapInit(Entity<MapGridComponent> grid, ref MapInitEvent args)
    {
        foreach (var tileRef in _map.GetAllTiles(grid, grid.Comp))
        {
            var def = _turf.GetContentTileDefinition(tileRef.Tile);
            if (def.Submerged)
                EnsureSource(tileRef, def);
        }
    }

    private void OnTileChanged(ref TileChangedEvent ev)
    {
        foreach (var change in ev.Changes)
        {
            if (!_map.TryGetTileRef(ev.Entity, ev.Entity.Comp, change.GridIndices, out var tileRef))
                continue;

            var def = _turf.GetContentTileDefinition(change.NewTile);
            if (def.Submerged)
            {
                EnsureSource(tileRef, def);
                continue;
            }

            // Not sea floor anymore: what's left is a normal puddle and drains.
            if (_puddle.TryGetPuddle(tileRef, out var puddle))
                RemComp<SubmergedPuddleComponent>(puddle);
        }
    }

    private void EnsureSource(TileRef tileRef, ContentTileDefinition def)
    {
        if (!_puddle.TryGetPuddle(tileRef, out var puddle)
            && !_puddle.TrySpillAt(tileRef, new Solution(def.SubmergedReagent, TileVolume), out puddle, false, false))
            return;

        EnsureComp<SubmergedPuddleComponent>(puddle).Reagent = def.SubmergedReagent;
        _puddle.WakePuddle(puddle);
    }
}
