using System.Numerics;
using Content.Client.Fluids;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using DrawDepth = Content.Shared.DrawDepth.DrawDepth;

namespace Content.Client._Floof.Flooding;

/// <summary>
///     With the local player under, shows the flood's shape on the floor: the deeper a tile's water the clearer it is,
///     and everything dry, space included, is darkest. Wet tiles mark the stencil, so one rectangle can cover the rest.
///     Under the sea, lattice and space are water too and stay clear, so only dry floors get darkened.
/// </summary>
public sealed partial class FloodDrySurfaceOverlay : Overlay
{
    [Dependency] private IEntityManager _entManager = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    private static readonly ProtoId<ShaderPrototype> Shader = "FloodFill";

    private readonly FloodWallSystem _system;
    private readonly PuddleSystem _puddle;
    private readonly SharedTransformSystem _xform;
    private readonly ShaderInstance _shader;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceEntities;

    public FloodDrySurfaceOverlay()
    {
        IoCManager.InjectDependencies(this);
        _system = _entManager.System<FloodWallSystem>();
        _puddle = _entManager.System<PuddleSystem>();
        _xform = _entManager.System<SharedTransformSystem>();
        _shader = _proto.Index(Shader).InstanceUnique();

        ZIndex = (int) DrawDepth.Puddles + 1;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_system.SubmergedView <= 0f)
            return;

        var handle = args.WorldHandle;
        var above = _system.AboveColor;

        _shader.Stencil = new StencilParameters
        {
            Enabled = true,
            Ref = _system.NextStencilRef(),
            Op = StencilOp.Replace,
            Func = StencilFunc.NotEqual,
        };
        handle.UseShader(_shader);

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
                handle.DrawRect(box, above.WithAlpha(above.A * (1f - tile.Shown)));
            }
        }

        handle.SetTransform(Matrix3x2.Identity);
        if (_puddle.TryGetSeaColor(args.MapId, out _))
            _system.DrawFloors(args, above);
        else
            handle.DrawRect(args.WorldBounds, above);

        handle.UseShader(null);
    }
}
