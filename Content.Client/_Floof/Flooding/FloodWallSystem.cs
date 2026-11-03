using System.Numerics;
using Content.Client.IconSmoothing;
using Content.Shared.Doors.Components;
using Content.Shared.Fluids.Components;
using Content.Shared.Maps;
using Content.Shared.Tag;
using Content.Shared.Wall;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._Floof.Flooding;

public enum FloodBarrier : byte
{
    None,
    Wall,
    Door,
}

/// <summary>
///     Tracks how high the liquid on each wet tile has crept up the walls next to it.
/// </summary>
public sealed partial class FloodWallSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlay = default!;
    [Dependency] private IResourceCache _cache = default!;
    [Dependency] private ITileDefinitionManager _tiles = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private TagSystem _tag = default!;

    private static readonly ProtoId<TagPrototype> WindowTag = "Window";
    private static readonly ResPath LatticeTexture = new("/Textures/Tiles/lattice.png");
    private const string LatticeTile = "Lattice";
    private const int LatticeVariantSize = 32;

    /// <summary>
    ///     Depth (0-1, see <see cref="SetLevel"/>) gained or lost per second, so the waterline visibly creeps.
    /// </summary>
    private const float CreepSpeed = 0.08f;

    /// <summary>
    ///     Below this depth the liquid is a film that doesn't reach the wall. About 1 cm.
    /// </summary>
    private const float MinDepth = 0.06f;

    private const float WaterlineLighten = 0.35f;
    private const float WaterlineAlphaBoost = 0.15f;

    /// <summary>
    ///     What lies above the water looks like from below it, when the local player is under.
    /// </summary>
    private const float AboveDarkness = 0.8f;
    private const float TintBrightness = 0.16f;
    private const float TintSaturation = 3f;
    private static readonly Color WaterHue = new(0.46f, 0.73f, 1f);

    /// <summary>
    ///     Each stencil user marks with its own value. 0 is the cleared buffer (or 255), 1 is used by the engine's stencil shaders.
    /// </summary>
    private const int MinStencilRef = 2;
    private const int MaxStencilRef = 254;

    private int _stencilRef = MinStencilRef;

    /// <summary>
    ///     How far the local player's view has turned to underwater, 0-1. See <see cref="FloodSubmergedSystem"/>.
    /// </summary>
    public float SubmergedView;

    /// <summary>
    ///     The colour of the liquid the local player is under, at full brightness.
    /// </summary>
    public Color SubmergedHue = WaterHue;

    /// <summary>
    ///     A deep, saturated version of the liquid. Water comes out dark blue, blood dark red.
    /// </summary>
    public Vector3 SubmergedTint => new(
        MathF.Pow(SubmergedHue.R, TintSaturation) * TintBrightness,
        MathF.Pow(SubmergedHue.G, TintSaturation) * TintBrightness,
        MathF.Pow(SubmergedHue.B, TintSaturation) * TintBrightness);

    public Vector3 CausticsColor => new(MathF.Sqrt(SubmergedHue.R), MathF.Sqrt(SubmergedHue.G), MathF.Sqrt(SubmergedHue.B));

    public Color AboveColor
    {
        get
        {
            var tint = SubmergedTint;
            return new Color(tint.X, tint.Y, tint.Z, AboveDarkness * SubmergedView);
        }
    }

    public readonly Dictionary<EntityUid, Dictionary<Vector2i, WetTile>> Grids = new();

    private readonly Dictionary<EntityUid, (EntityUid Grid, Vector2i Tile)> _puddles = new();
    private readonly List<Vector2i> _dried = new();
    private List<Entity<MapGridComponent>> _seaGrids = new();

    public sealed class WetTile
    {
        public float Target;
        public float Shown;
        public Color Color;
        public float Opacity;

        /// <summary>
        ///     The current on this tile as last measured, and eased towards it for drawing. See <see cref="FloodFlowSystem"/>.
        /// </summary>
        public Vector2 FlowTarget;
        public Vector2 Flow;
        public float FlowPhase;

        /// <summary>
        ///     Seconds this tile has been far enough inside the local player's view for its neighbours to be known.
        /// </summary>
        public float InRange;
    }

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PuddleComponent, ComponentShutdown>(OnPuddleShutdown);

        _overlay.AddOverlay(new FloodOceanTileOverlay());
        _overlay.AddOverlay(new FloodSeaOverlay());
        _overlay.AddOverlay(new FloodDrySurfaceOverlay());
        _overlay.AddOverlay(new FloodWallOverlay());
        _overlay.AddOverlay(new FloodDoorOverlay());
        _overlay.AddOverlay(new FloodObjectOverlay());
        _overlay.AddOverlay(new FloodItemOverlay());
        _overlay.AddOverlay(new FloodMobOverlay());
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _overlay.RemoveOverlay<FloodOceanTileOverlay>();
        _overlay.RemoveOverlay<FloodSeaOverlay>();
        _overlay.RemoveOverlay<FloodDrySurfaceOverlay>();
        _overlay.RemoveOverlay<FloodWallOverlay>();
        _overlay.RemoveOverlay<FloodDoorOverlay>();
        _overlay.RemoveOverlay<FloodObjectOverlay>();
        _overlay.RemoveOverlay<FloodItemOverlay>();
        _overlay.RemoveOverlay<FloodMobOverlay>();
    }

    public int NextStencilRef()
    {
        _stencilRef = _stencilRef >= MaxStencilRef ? MinStencilRef : _stencilRef + 1;
        return _stencilRef;
    }

    /// <summary>
    ///     Draws over everything that's floor in view, wet or dry.
    /// </summary>
    /// <param name="skip">A grid to leave out, like a planet map's own ground.</param>
    public void DrawFloors(in OverlayDrawArgs args, Color color, EntityUid? skip = null)
    {
        var handle = args.WorldHandle;

        _seaGrids.Clear();
        _map.FindGridsIntersecting(args.MapId, args.WorldAABB, ref _seaGrids);
        foreach (var grid in _seaGrids)
        {
            if (grid.Owner == skip)
                continue;

            handle.SetTransform(_xform.GetWorldMatrix(grid));

            foreach (var tile in _map.GetTilesIntersecting(grid, grid, args.WorldAABB))
            {
                if (!_turf.IsSpace(tile))
                    handle.DrawRect(new Box2(tile.GridIndices, tile.GridIndices + Vector2i.One), color);
            }
        }

        handle.SetTransform(Matrix3x2.Identity);
    }

    /// <summary>
    ///     Draws over all that isn't floor: lattice, space and whatever's past the grid. Floors mark the stencil through
    ///     <paramref name="mask"/>, so one rectangle can cover the rest.
    /// </summary>
    public void DrawSea(in OverlayDrawArgs args, ShaderInstance mask, ShaderInstance fill, Color color)
    {
        var stencil = new StencilParameters
        {
            Enabled = true,
            Ref = NextStencilRef(),
            Op = StencilOp.Replace,
            Func = StencilFunc.NotEqual,
        };
        mask.Stencil = stencil;
        fill.Stencil = stencil;

        var handle = args.WorldHandle;
        handle.UseShader(mask);
        DrawFloors(args, Color.Transparent);

        handle.UseShader(fill);
        handle.DrawRect(args.WorldBounds, color);
    }

    /// <summary>
    ///     Water covers the tile sprites of lattice, as it counts as open sea. This puts the bars back on top.
    /// </summary>
    public void DrawLattice(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var texture = _cache.GetResource<TextureResource>(LatticeTexture).Texture;
        var lattice = _tiles[LatticeTile].TileId;

        _seaGrids.Clear();
        _map.FindGridsIntersecting(args.MapId, args.WorldAABB, ref _seaGrids);
        foreach (var grid in _seaGrids)
        {
            handle.SetTransform(_xform.GetWorldMatrix(grid));

            foreach (var tile in _map.GetTilesIntersecting(grid, grid, args.WorldAABB))
            {
                if (tile.Tile.TypeId != lattice)
                    continue;

                var left = tile.Tile.Variant * LatticeVariantSize;
                handle.DrawTextureRectRegion(texture,
                    new Box2(tile.GridIndices, tile.GridIndices + Vector2i.One),
                    null,
                    new UIBox2(left, 0, left + LatticeVariantSize, LatticeVariantSize));
            }
        }

        handle.SetTransform(Matrix3x2.Identity);
    }

    public FloodBarrier GetBarrier(EntityUid ent)
    {
        // Directional windows and windoors don't smooth and don't fill the tile.
        var smooths = HasComp<IconSmoothComponent>(ent);
        if (HasComp<WallComponent>(ent) || smooths && _tag.HasTag(ent, WindowTag))
            return FloodBarrier.Wall;

        if (smooths && TryComp<DoorComponent>(ent, out var door) &&
            door.State is not (DoorState.Open or DoorState.Opening))
        {
            return FloodBarrier.Door;
        }

        return FloodBarrier.None;
    }

    public static Color WaterlineColor(Color color)
    {
        return Color.InterpolateBetween(color, Color.White, WaterlineLighten)
            .WithAlpha(MathF.Min(color.A + WaterlineAlphaBoost, 1f));
    }

    /// <param name="depth">0 for a dry tile, 1 for a full one.</param>
    /// <param name="opacity">The liquid's own opacity at a full tile.</param>
    public void SetLevel(EntityUid puddle, float depth, Color color, float opacity)
    {
        var xform = Transform(puddle);
        if (xform.GridUid is not { } grid || !TryComp<MapGridComponent>(grid, out var gridComp))
            return;

        var indices = _map.TileIndicesFor(grid, gridComp, xform.Coordinates);
        _puddles[puddle] = (grid, indices);

        if (!Grids.TryGetValue(grid, out var tiles))
            Grids[grid] = tiles = new Dictionary<Vector2i, WetTile>();

        if (!tiles.TryGetValue(indices, out var tile))
        {
            // Only the level changing makes it creep. This could just be the first we're hearing of an old puddle.
            tiles[indices] = tile = new WetTile { Shown = depth >= MinDepth ? depth : 0f };
        }

        tile.Target = depth >= MinDepth ? depth : 0f;
        tile.Color = color;
        tile.Opacity = opacity;
    }

    private void OnPuddleShutdown(Entity<PuddleComponent> entity, ref ComponentShutdown args)
    {
        if (_puddles.Remove(entity, out var key) &&
            Grids.TryGetValue(key.Grid, out var tiles) &&
            tiles.TryGetValue(key.Tile, out var tile))
        {
            tile.Target = 0f;
        }
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var step = CreepSpeed * frameTime;
        foreach (var (grid, tiles) in Grids)
        {
            foreach (var (indices, tile) in tiles)
            {
                tile.Shown = tile.Shown < tile.Target
                    ? MathF.Min(tile.Shown + step, tile.Target)
                    : MathF.Max(tile.Shown - step, tile.Target);

                if (tile.Shown <= 0f && tile.Target <= 0f)
                    _dried.Add(indices);
            }

            foreach (var indices in _dried)
            {
                tiles.Remove(indices);
            }

            _dried.Clear();
        }
    }
}
