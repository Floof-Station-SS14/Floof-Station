using System.Numerics;
using Content.Shared.Maps;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._Floof.Ocean;

/// <summary>
///     Tiles can't animate, so open water on a planet map gets its ripples drawn over it, around whatever floors are
///     floating there.
/// </summary>
public sealed partial class OceanSurfaceOverlay : Overlay
{
    [Dependency] private IEntityManager _entManager = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IResourceCache _cache = default!;
    [Dependency] private ITileDefinitionManager _tiles = default!;

    private const string OceanTile = "FloorOceanSurface";
    private const string WaterState = "water";
    private const string LatticeTile = "Lattice";
    private const int LatticeVariantSize = 32;
    private const int MinStencilRef = 2;
    private const int MaxStencilRef = 254;
    private static readonly ResPath WaterRsi = new("/Textures/_Floof/Tiles/ocean_surface.rsi");
    private static readonly ResPath LatticeTexture = new("/Textures/Tiles/lattice.png");
    private static readonly ProtoId<ShaderPrototype> Shader = "OceanFill";

    private readonly SharedMapSystem _map;
    private readonly SharedTransformSystem _xform;
    private readonly TurfSystem _turf;
    private readonly ShaderInstance _mask;
    private readonly ShaderInstance _fill;
    private readonly ushort _oceanTile;
    private readonly ushort _latticeTile;
    private readonly Texture[] _frames;
    private readonly float _frameTime;
    private List<Entity<MapGridComponent>> _grids = new();
    private int _stencilRef = MinStencilRef;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowEntities;

    public OceanSurfaceOverlay()
    {
        IoCManager.InjectDependencies(this);
        _map = _entManager.System<SharedMapSystem>();
        _xform = _entManager.System<SharedTransformSystem>();
        _turf = _entManager.System<TurfSystem>();
        _mask = _proto.Index(Shader).InstanceUnique();
        _fill = _proto.Index(Shader).InstanceUnique();
        _oceanTile = _tiles[OceanTile].TileId;
        _latticeTile = _tiles[LatticeTile].TileId;

        var rsi = _cache.GetResource<RSIResource>(WaterRsi).RSI;
        rsi.TryGetState(WaterState, out var state);
        _frames = state!.GetFrames(RsiDirection.South);
        _frameTime = state.GetDelay(0);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var map = _map.GetMapOrInvalid(args.MapId);
        if (!_entManager.TryGetComponent<MapGridComponent>(map, out var ground))
            return;

        // A new ref each draw, so marks left in the stencil from earlier frames don't count.
        _stencilRef = _stencilRef >= MaxStencilRef ? MinStencilRef : _stencilRef + 1;
        var stencil = new StencilParameters
        {
            Enabled = true,
            Ref = _stencilRef,
            Op = StencilOp.Replace,
            Func = StencilFunc.NotEqual,
        };
        _mask.Stencil = stencil;
        _fill.Stencil = stencil;

        var handle = args.WorldHandle;
        _grids.Clear();
        _map.FindGridsIntersecting(args.MapId, args.WorldAABB, ref _grids);

        // Floors of grids floating on the water mark the stencil, so the ripples stay off them.
        handle.UseShader(_mask);
        foreach (var grid in _grids)
        {
            if (grid.Owner == map)
                continue;

            handle.SetTransform(_xform.GetWorldMatrix(grid));
            foreach (var tile in _map.GetTilesIntersecting(grid, grid, args.WorldAABB))
            {
                if (!_turf.IsSpace(tile))
                    handle.DrawRect(new Box2(tile.GridIndices, tile.GridIndices + Vector2i.One), Color.Transparent);
            }
        }

        var frame = _frames[(int) (_timing.RealTime.TotalSeconds / _frameTime) % _frames.Length];
        handle.UseShader(_fill);
        handle.SetTransform(_xform.GetWorldMatrix(map));
        foreach (var tile in _map.GetTilesIntersecting(map, ground, args.WorldAABB))
        {
            if (tile.Tile.TypeId == _oceanTile)
                handle.DrawTextureRect(frame, new Box2(tile.GridIndices, tile.GridIndices + Vector2i.One));
        }

        handle.UseShader(null);
        DrawLattice(args);
        handle.SetTransform(Matrix3x2.Identity);
    }

    /// <summary>
    ///     Lattice counts as open water, so the ripples cover its sprite. This puts the bars back on top.
    /// </summary>
    private void DrawLattice(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var texture = _cache.GetResource<TextureResource>(LatticeTexture).Texture;

        foreach (var grid in _grids)
        {
            handle.SetTransform(_xform.GetWorldMatrix(grid));
            foreach (var tile in _map.GetTilesIntersecting(grid, grid, args.WorldAABB))
            {
                if (tile.Tile.TypeId != _latticeTile)
                    continue;

                var left = tile.Tile.Variant * LatticeVariantSize;
                handle.DrawTextureRectRegion(texture,
                    new Box2(tile.GridIndices, tile.GridIndices + Vector2i.One),
                    null,
                    new UIBox2(left, 0, left + LatticeVariantSize, LatticeVariantSize));
            }
        }
    }
}
