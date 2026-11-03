using Content.Client.Fluids;
using Content.Shared._Floof.Flooding;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using DrawDepth = Content.Shared.DrawDepth.DrawDepth;

namespace Content.Client._Floof.Flooding;

/// <summary>
///     Under the sea, everything that isn't floor looks like a full tile of water: lattice, space and whatever's past the
///     grid.
/// </summary>
public sealed partial class FloodSeaOverlay : Overlay
{
    [Dependency] private IEntityManager _entManager = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IResourceCache _cache = default!;

    private static readonly ProtoId<ShaderPrototype> Shader = "FloodFill";
    private static readonly ProtoId<ShaderPrototype> DeepShader = "FloodDeep";
    private static readonly ResPath FloorTexture = new("/Textures/Tiles/Asteroid/asteroid.png");
    private static readonly ResPath SurfaceTexture = new("/Textures/_Floof/Tiles/ocean_surface.rsi/water.png");

    // Keeps time with FloodOceanTileOverlay, so the ripples carry on across the move between maps.
    private const int SurfaceFrames = 4;
    private const float SurfaceFrameTime = 0.35f;

    private readonly SharedMapSystem _map;
    private readonly FloodWallSystem _system;
    private readonly PuddleSystem _puddle;
    private readonly ShaderInstance _shader;
    private readonly ShaderInstance _deepShader;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceEntities;

    public FloodSeaOverlay()
    {
        IoCManager.InjectDependencies(this);
        _map = _entManager.System<SharedMapSystem>();
        _system = _entManager.System<FloodWallSystem>();
        _puddle = _entManager.System<PuddleSystem>();
        _shader = _proto.Index(Shader).InstanceUnique();
        _deepShader = _proto.Index(DeepShader).InstanceUnique();

        ZIndex = (int) DrawDepth.Puddles + 1;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (!_puddle.TryGetSeaColor(args.MapId, out var color))
            return;

        var map = _map.GetMapOrInvalid(args.MapId);
        if (_entManager.TryGetComponent<FloodDepthComponent>(map, out var depth) && args.Viewport.Eye is { } eye)
        {
            var scale = (args.Viewport.WorldToLocal(eye.Position.Position + System.Numerics.Vector2.UnitX) -
                         args.Viewport.WorldToLocal(eye.Position.Position)).Length();
            var origin = args.Viewport.WorldToLocal(System.Numerics.Vector2.Zero);
            _deepShader.SetParameter("world_origin", new System.Numerics.Vector2(origin.X, args.Viewport.Size.Y - origin.Y));
            _deepShader.SetParameter("tile_pixels", scale);
            _deepShader.SetParameter("depth", depth.GetDepth(_timing.CurTime));
            _deepShader.SetParameter("brightness", depth.GetBrightness(_timing.CurTime));
            _deepShader.SetParameter("travel", depth.GetTravel(_timing.CurTime));
            _deepShader.SetParameter("view_center",(System.Numerics.Vector2) args.Viewport.Size / 2f);
            _deepShader.SetParameter("floor_texture", _cache.GetResource<TextureResource>(FloorTexture).Texture);
            _deepShader.SetParameter("submerged", depth.GetSubmerged(_timing.CurTime));
            // Not as a Color, which would get converted while the map's light doesn't.
            _deepShader.SetParameter("surface_light", new System.Numerics.Vector3(depth.SurfaceLight.R, depth.SurfaceLight.G, depth.SurfaceLight.B));
            _deepShader.SetParameter("surface_frame", (float) ((int) (_timing.RealTime.TotalSeconds / SurfaceFrameTime) % SurfaceFrames));
            _deepShader.SetParameter("surface_texture", _cache.GetResource<TextureResource>(SurfaceTexture).Texture);
            _system.DrawSea(args, _shader, _deepShader, Color.White);
        }
        else
        {
            _system.DrawSea(args, _shader, _shader, color);
        }

        args.WorldHandle.UseShader(null);
        _system.DrawLattice(args);
    }
}
