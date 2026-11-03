using System.Numerics;
using Content.Client.Fluids;
using Content.Shared._Floof.Flooding;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Floof.Flooding;

/// <summary>
///     With the local player under, wet tiles ripple and carry the moving web of light seen on the floor of a sunlit pool.
///     Deeper water shows more of it. Drawn before the screen tint, so that tints it too.
/// </summary>
public sealed partial class FloodCausticsOverlay : Overlay
{
    [Dependency] private IEntityManager _entManager = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    private static readonly ProtoId<ShaderPrototype> Shader = "FloodCaustics";
    private static readonly ProtoId<ShaderPrototype> MaskShader = "FloodFill";

    private readonly FloodWallSystem _system;
    private readonly PuddleSystem _puddle;
    private readonly SharedMapSystem _map;
    private readonly SharedTransformSystem _xform;
    private readonly ShaderInstance _shader;
    private readonly ShaderInstance _seaShader;
    private readonly ShaderInstance _seaMask;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;
    public override bool RequestScreenTexture => true;

    public FloodCausticsOverlay()
    {
        IoCManager.InjectDependencies(this);
        _system = _entManager.System<FloodWallSystem>();
        _puddle = _entManager.System<PuddleSystem>();
        _map = _entManager.System<SharedMapSystem>();
        _xform = _entManager.System<SharedTransformSystem>();
        _shader = _proto.Index(Shader).InstanceUnique();
        _seaShader = _proto.Index(Shader).InstanceUnique();
        _seaMask = _proto.Index(MaskShader).InstanceUnique();

        ZIndex = -2;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return _system.SubmergedView > 0f;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null || args.Viewport.Eye is not { } eye)
            return;

        var handle = args.WorldHandle;
        var origin = args.Viewport.WorldToLocal(Vector2.Zero);
        var scale = (args.Viewport.WorldToLocal(eye.Position.Position + Vector2.UnitX) -
                     args.Viewport.WorldToLocal(eye.Position.Position)).Length();

        _shader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _shader.SetParameter("world_origin", new Vector2(origin.X, args.Viewport.Size.Y - origin.Y));
        _shader.SetParameter("tile_pixels", scale);
        _shader.SetParameter("light_color", _system.CausticsColor);
        handle.UseShader(_shader);

        // Sunlight only: what's out in the open fades with it, while the station's flooded rooms have their own lights.
        var daylight = _entManager.TryGetComponent<FloodDepthComponent>(_map.GetMapOrInvalid(args.MapId), out var depth)
            ? depth.GetBrightness(_timing.CurTime)
            : 1f;
        var strength = Color.White.WithAlpha(_system.SubmergedView * daylight);

        foreach (var (grid, tiles) in _system.Grids)
        {
            if (!_entManager.HasComponent<MapGridComponent>(grid))
                continue;

            var (_, _, matrix, invMatrix) = _xform.GetWorldPositionRotationMatrixWithInv(grid);
            handle.SetTransform(matrix);
            var bounds = invMatrix.TransformBox(args.WorldBounds).Enlarged(1f);

            foreach (var (indices, tile) in tiles)
            {
                if (tile.Shown <= 0f || !bounds.Contains(indices))
                    continue;

                var box = new Box2(indices, indices + Vector2i.One);
                handle.DrawRect(box, Color.White.WithAlpha(tile.Shown * _system.SubmergedView));
            }
        }

        handle.SetTransform(Matrix3x2.Identity);

        if (daylight > 0f && _puddle.TryGetSeaColor(args.MapId, out _))
        {
            _seaShader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
            _seaShader.SetParameter("world_origin", new Vector2(origin.X, args.Viewport.Size.Y - origin.Y));
            _seaShader.SetParameter("tile_pixels", scale);
            _seaShader.SetParameter("light_color", _system.CausticsColor);
            _system.DrawSea(args, _seaMask, _seaShader, strength);
        }

        handle.UseShader(null);
    }
}
