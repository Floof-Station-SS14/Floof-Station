using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._Floof.Flooding;

/// <summary>
///     Tiles can't animate, so open water on a planet map gets its ripples drawn over it, around whatever floors are
///     floating there.
/// </summary>
public sealed partial class FloodOceanTileOverlay : Overlay
{
    [Dependency] private IEntityManager _entManager = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IResourceCache _cache = default!;
    [Dependency] private ITileDefinitionManager _tiles = default!;

    private const string OceanTile = "FloorOceanSurface";
    private const string WaterState = "water";
    private static readonly ResPath WaterRsi = new("/Textures/_Floof/Tiles/ocean_surface.rsi");
    private static readonly ProtoId<ShaderPrototype> Shader = "FloodFill";

    private readonly SharedMapSystem _map;
    private readonly SharedTransformSystem _xform;
    private readonly FloodWallSystem _system;
    private readonly ShaderInstance _mask;
    private readonly ShaderInstance _fill;
    private readonly ushort _oceanTile;
    private readonly Texture[] _frames;
    private readonly float _frameTime;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowEntities;

    public FloodOceanTileOverlay()
    {
        IoCManager.InjectDependencies(this);
        _map = _entManager.System<SharedMapSystem>();
        _xform = _entManager.System<SharedTransformSystem>();
        _system = _entManager.System<FloodWallSystem>();
        _mask = _proto.Index(Shader).InstanceUnique();
        _fill = _proto.Index(Shader).InstanceUnique();
        _oceanTile = _tiles[OceanTile].TileId;

        var rsi = _cache.GetResource<RSIResource>(WaterRsi).RSI;
        rsi.TryGetState(WaterState, out var state);
        _frames = state!.GetFrames(RsiDirection.South);
        _frameTime = state.GetDelay(0);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var map = _map.GetMapOrInvalid(args.MapId);
        if (!_entManager.TryGetComponent<MapGridComponent>(map, out var grid))
            return;

        var stencil = new StencilParameters
        {
            Enabled = true,
            Ref = _system.NextStencilRef(),
            Op = StencilOp.Replace,
            Func = StencilFunc.NotEqual,
        };
        _mask.Stencil = stencil;
        _fill.Stencil = stencil;

        var handle = args.WorldHandle;
        handle.UseShader(_mask);
        _system.DrawFloors(args, Color.Transparent, map);

        var frame = _frames[(int) (_timing.RealTime.TotalSeconds / _frameTime) % _frames.Length];
        handle.UseShader(_fill);
        handle.SetTransform(_xform.GetWorldMatrix(map));
        foreach (var tile in _map.GetTilesIntersecting(map, grid, args.WorldAABB))
        {
            if (tile.Tile.TypeId == _oceanTile)
                handle.DrawTextureRect(frame, new Box2(tile.GridIndices, tile.GridIndices + Vector2i.One));
        }

        handle.SetTransform(System.Numerics.Matrix3x2.Identity);
        handle.UseShader(null);
        _system.DrawLattice(args);
    }
}
