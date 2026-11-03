using Content.Shared.Atmos.Components;
using Content.Shared._Floof.Flooding;
using Content.Shared.Atmos;
using Robust.Shared.Map.Components;

namespace Content.Server.Atmos.EntitySystems;

/// <summary>
///     Water stops gas like a wall would: a tile full of it holds on to its air until it drains, and under the sea,
///     what's open to the map is water, so nothing vents into it.
/// </summary>
public sealed partial class AtmosphereSystem
{
    [Dependency] private EntityQuery<FloodAmbientPressureComponent> _floodAmbientQuery = default!;

    private void InitializeFlood()
    {
        SubscribeLocalEvent<FloodAmbientPressureComponent, ComponentStartup>(OnSeaChanged);
        SubscribeLocalEvent<FloodAmbientPressureComponent, ComponentShutdown>(OnSeaChanged);
    }

    private void OnSeaChanged<T>(Entity<FloodAmbientPressureComponent> ent, ref T args)
    {
        var grids = EntityQueryEnumerator<GridAtmosphereComponent, MapGridComponent, TransformComponent>();
        while (grids.MoveNext(out var uid, out var atmos, out _, out var xform))
        {
            if (xform.MapUid != ent.Owner)
                continue;

            foreach (var tile in atmos.MapTiles)
            {
                InvalidateTile((uid, atmos), tile.GridIndices);
            }
        }
    }

    /// <summary>
    ///     Marks a tile as full of liquid, or not anymore.
    /// </summary>
    public void SetFloodFull(EntityUid grid, Vector2i indices, bool full)
    {
        if (!_gridAtmosQuery.TryComp(grid, out var atmos) || !atmos.Tiles.TryGetValue(indices, out var tile))
            return;

        tile.FloodFull = full;
        InvalidateTile((grid, atmos), indices);
    }

    /// <summary>
    ///     Adds every tile bordering one open to the map, with no wall, closed door and so on in between. Once per side
    ///     it's open on, so a tile with more openings fills faster.
    /// </summary>
    public void GetFloodBreaches(GridAtmosphereComponent atmos, List<Vector2i> breaches)
    {
        foreach (var outside in atmos.MapTiles)
        {
            for (var i = 0; i < Atmospherics.Directions; i++)
            {
                var direction = (AtmosDirection) (1 << i);
                if (outside.AirtightData.BlockedDirections.IsFlagSet(direction) ||
                    !atmos.Tiles.TryGetValue(outside.GridIndices.Offset(direction), out var inside) ||
                    inside.MapAtmosphere ||
                    inside.AirtightData.BlockedDirections.IsFlagSet(direction.GetOpposite()))
                    continue;

                breaches.Add(inside.GridIndices);
            }
        }
    }

    private void UpdateFloodSeal(GridAtmosphereComponent atmos, TileAtmosphere tile, bool exposed, EntityUid? map)
    {
        tile.FloodExposed = exposed && _floodAmbientQuery.HasComp(map);

        // A group spanning the new seal would keep sharing across it.
        if (GetFloodBlocked(tile) != tile.AirtightData.BlockedDirections && tile.ExcitedGroup != null)
            ExcitedGroupDispose(atmos, tile.ExcitedGroup);
    }

    /// <summary>
    ///     The directions gas can't leave this tile in.
    /// </summary>
    private static AtmosDirection GetFloodBlocked(TileAtmosphere tile)
    {
        return tile.FloodFull || tile.FloodExposed ? AtmosDirection.All : tile.AirtightData.BlockedDirections;
    }
}
