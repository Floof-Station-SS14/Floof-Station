using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Content.Shared._Floof.CCVars;
using Content.Shared.ActionBlocker;
using Content.Shared.Buckle.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Climbing.Components;
using Content.Shared.Clothing;
using Content.Shared.Damage.Components;
using Content.Shared.Doors.Components;
using Content.Shared.Fluids;
using Content.Shared.Fluids.Components;
using Content.Shared.Ghost.Components;
using Content.Shared.Gravity;
using Content.Shared.Inventory;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Maps;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Physics;
using Content.Shared.Standing;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Controllers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Floof.Flooding;

/// <summary>
///     Deep liquid slows mobs down, carries them along with its current and makes them swim in strokes.
///     Both sides work it out from the puddle volumes clients already get for drawing, so it needs no state of its own
///     and only costs anything on tiles somebody is standing in.
/// </summary>
public sealed partial class FloodWaterSystem : VirtualController
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private ActionBlockerSystem _blocker = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private ItemToggleSystem _toggle = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MovementSpeedModifierSystem _speed = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedGravitySystem _gravity = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private TurfSystem _turf = default!;

    [Dependency] private EntityQuery<BuckleComponent> _buckleQuery = default!;
    [Dependency] private EntityQuery<ClimbingComponent> _climbingQuery = default!;
    [Dependency] private EntityQuery<FloodAmbientPressureComponent> _ambientQuery = default!;
    [Dependency] private EntityQuery<FloodMaskComponent> _maskQuery = default!;
    [Dependency] private EntityQuery<FloodSwimmerComponent> _swimmerQuery = default!;
    [Dependency] private EntityQuery<DoorComponent> _doorQuery = default!;
    [Dependency] private EntityQuery<FloodSinkingComponent> _sinkingQuery = default!;
    [Dependency] private EntityQuery<MagbootsComponent> _magbootsQuery = default!;
    [Dependency] private EntityQuery<StaminaComponent> _staminaQuery = default!;
    [Dependency] private EntityQuery<MapGridComponent> _gridQuery = default!;
    [Dependency] private EntityQuery<PuddleComponent> _puddleQuery = default!;

    private static readonly Vector2i[] Neighbors = { Vector2i.Up, Vector2i.Down, Vector2i.Left, Vector2i.Right };

    private static readonly SoundSpecifier StrokeSound = new SoundCollectionSpecifier("FloodSwimStroke");

    /// <summary>
    ///     Speed changes smaller than this aren't worth refreshing the mob's movement for.
    /// </summary>
    private const float SpeedFactorStep = 0.05f;

    private float _slowStart;
    private float _slowEnd;
    private float _slowSpeed;
    private float _sweepDepth;
    private float _sweepSurge;
    private float _sweepCalm;
    private float _currentScale;
    private float _currentMaxSpeed;
    private float _currentAccel;
    private float _currentDeadzone;
    private float _currentSmoothing;
    private float _swimDepth;
    private bool _lyingSwim;
    private float _lyingSwimDepth;
    private float _lyingStrokeSpeed;
    private float _strokeInterval;
    private float _strokeTime;
    private float _strokeSpeed;
    private float _strokeAccel;
    private float _glideFriction;
    private float _drownDepth;
    public float DrownDepth => _drownDepth;
    private float _pressureDepth;
    private float _sinkStroke;

    public override void Initialize()
    {
        UpdatesAfter.Add(typeof(SharedMoverController));

        SubscribeLocalEvent<MobMoverComponent, MapInitEvent>(OnMobMapInit);
        SubscribeLocalEvent<FloodSwimmerComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
        SubscribeLocalEvent<FloodSwimmerComponent, IsWeightlessEvent>(OnIsWeightless);

        Subs.CVar(_cfg, CCVars.CCVars.FloodSlowStartDepth, value => _slowStart = value, true);
        Subs.CVar(_cfg, CCVars.CCVars.FloodSlowEndDepth, value => _slowEnd = value, true);
        Subs.CVar(_cfg, CCVars.CCVars.FloodSlowSpeed, value => _slowSpeed = value, true);
        Subs.CVar(_cfg, CCVars.CCVars.FloodSweepDepth, value => _sweepDepth = value, true);
        Subs.CVar(_cfg, CCVars.CCVars.FloodSweepSurge, value => _sweepSurge = value, true);
        Subs.CVar(_cfg, CCVars.CCVars.FloodSweepCalm, value => _sweepCalm = value, true);
        Subs.CVar(_cfg, CCVars.CCVars.FloodCurrentScale, value => _currentScale = value, true);
        Subs.CVar(_cfg, CCVars.CCVars.FloodCurrentMaxSpeed, value => _currentMaxSpeed = value, true);
        Subs.CVar(_cfg, CCVars.CCVars.FloodCurrentAcceleration, value => _currentAccel = value, true);
        Subs.CVar(_cfg, CCVars.CCVars.FloodCurrentDeadzone, value => _currentDeadzone = value, true);
        Subs.CVar(_cfg, CCVars.CCVars.FloodCurrentSmoothing, value => _currentSmoothing = value, true);
        Subs.CVar(_cfg, CCVars.CCVars.FloodSwimDepth, value => _swimDepth = value, true);
        Subs.CVar(_cfg, CCVars.CCVars.FloodLyingSwim, value => _lyingSwim = value, true);
        Subs.CVar(_cfg, CCVars.CCVars.FloodLyingSwimDepth, value => _lyingSwimDepth = value, true);
        Subs.CVar(_cfg, CCVars.CCVars.FloodLyingStrokeSpeed, value => _lyingStrokeSpeed = value, true);
        Subs.CVar(_cfg, CCVars.CCVars.FloodStrokeInterval, value => _strokeInterval = value, true);
        Subs.CVar(_cfg, CCVars.CCVars.FloodStrokeTime, value => _strokeTime = value, true);
        Subs.CVar(_cfg, CCVars.CCVars.FloodStrokeSpeed, value => _strokeSpeed = value, true);
        Subs.CVar(_cfg, CCVars.CCVars.FloodStrokeAcceleration, value => _strokeAccel = value, true);
        Subs.CVar(_cfg, CCVars.CCVars.FloodGlideFriction, value => _glideFriction = value, true);
        Subs.CVar(_cfg, CCVars.CCVars.FloodDrownDepth, value => _drownDepth = value, true);
        Subs.CVar(_cfg, CCVars.CCVars.FloodPressureDepth, value => _pressureDepth = value, true);
        Subs.CVar(_cfg, CCVars.CCVars.FloodSinkStroke, value => _sinkStroke = value, true);

        base.Initialize();
    }

    private void OnMobMapInit(Entity<MobMoverComponent> ent, ref MapInitEvent args)
    {
        if (!IsIntangible(ent))
            EnsureComp<FloodSwimmerComponent>(ent);
    }

    private void OnRefreshSpeed(Entity<FloodSwimmerComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        args.ModifySpeed(ent.Comp.SpeedFactor);
    }

    /// <summary>
    ///     Past the edge of the grid nothing gives you gravity, but you still swim out there. Parent changes refresh this.
    /// </summary>
    private void OnIsWeightless(Entity<FloodSwimmerComponent> ent, ref IsWeightlessEvent args)
    {
        var xform = Transform(ent);
        if (args.Handled || xform.GridUid != null || !IsInSea(xform))
            return;

        args.IsWeightless = false;
        args.Handled = true;
    }

    public override void UpdateBeforeSolve(bool prediction, float frameTime)
    {
        base.UpdateBeforeSolve(prediction, frameTime);

        var query = EntityQueryEnumerator<FloodSwimmerComponent, PhysicsComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var swimmer, out var physics, out var xform))
        {
            if (prediction && !physics.Predict)
                continue;

            var volume = GetVolume(xform);
            var depth = GetStandingDepth(uid, xform);
            UpdateSlowdown((uid, swimmer), depth);
            ApplyCurrent((uid, swimmer), physics, xform, volume, depth, frameTime);
        }
    }

    private void UpdateSlowdown(Entity<FloodSwimmerComponent> ent, float depth)
    {
        var factor = 1f;
        if (depth >= _slowStart && (depth < GetSwimDepth(ent) || IsOnBottom(ent)))
        {
            var progress = Math.Clamp((depth - _slowStart) / MathF.Max(_slowEnd - _slowStart, 0.001f), 0f, 1f);
            factor = MathHelper.Lerp(1f, _slowSpeed, progress);
        }

        factor = MathF.Round(factor / SpeedFactorStep) * SpeedFactorStep;
        if (MathHelper.CloseTo(ent.Comp.SpeedFactor, factor))
            return;

        ent.Comp.SpeedFactor = factor;
        Dirty(ent);
        _speed.RefreshMovementSpeedModifiers(ent.Owner);
    }

    private void ApplyCurrent(Entity<FloodSwimmerComponent> ent, PhysicsComponent physics, TransformComponent xform, float volume, float depth, float frameTime)
    {
        var uid = ent.Owner;
        var free = depth >= _sweepDepth &&
                   physics.BodyStatus == BodyStatus.OnGround &&
                   xform.GridUid != null &&
                   xform.GridUid != xform.MapUid &&
                   !TryGetSwimTile(xform, out _) &&
                   !_gravity.IsWeightless(uid) &&
                   !(_buckleQuery.TryComp(uid, out var buckle) && buckle.Buckled) &&
                   !IsOnBottom(uid);

        var measured = Vector2.Zero;
        if (free)
        {
            var measuredGrid = _gridQuery.GetComponent(xform.GridUid!.Value);
            measured = GetCurrent(xform.GridUid.Value, measuredGrid, _map.TileIndicesFor(xform.GridUid.Value, measuredGrid, xform.Coordinates), volume);
        }

        // Puddles only change about once per flow, so the mob eases towards what it reads. Resimulated ticks keep the
        // value they have, or the client would ease faster than the server.
        if (_timing.IsFirstTimePredicted)
        {
            var blend = 1f - MathF.Exp(-frameTime / MathF.Max(_currentSmoothing, 0.001f));
            ent.Comp.Current = Vector2.Lerp(ent.Comp.Current, measured, blend);
            if (measured == Vector2.Zero && ent.Comp.Current.LengthSquared() < 0.0001f)
                ent.Comp.Current = Vector2.Zero;
        }

        var current = ent.Comp.Current;
        if (!free || current == Vector2.Zero)
            return;

        var gridUid = xform.GridUid!.Value;

        // Shallow water takes a real surge to move you, deep water drags you along with hardly any current.
        var progress = Math.Clamp((depth - _sweepDepth) / MathF.Max(_swimDepth - _sweepDepth, 0.001f), 0f, 1f);
        var strength = current.Length();
        // Measured from the threshold, so the push fades in rather than switching on.
        var excess = strength - MathHelper.Lerp(_sweepSurge, _sweepCalm, progress);
        if (excess <= 0f)
            return;

        var direction = _transform.GetWorldRotation(gridUid).RotateVec(current / strength);
        var velocity = physics.LinearVelocity;
        SharedMoverController.Accelerate(ref velocity, direction * MathF.Min(excess * _currentScale, _currentMaxSpeed), _currentAccel, frameTime);
        PhysicsSystem.SetLinearVelocity(uid, velocity, body: physics);
    }

    /// <summary>
    ///     Swaps walking for strokes in deep liquid: holding a direction pushes off every so often and glides in between.
    /// </summary>
    /// <returns>Whether the mob is swimming, and so isn't taking any steps.</returns>
    public bool TrySwim(EntityUid uid, TransformComponent xform, ref Vector2 wishDir, ref float friction, ref float accel)
    {
        if (!TryComp<FloodSwimmerComponent>(uid, out var swimmer))
            return false;

        var depth = GetStandingDepth(uid, xform);
        if (depth < GetSwimDepth(uid) || IsOnBottom(uid))
            return false;

        friction = _glideFriction;
        var direction = wishDir;
        wishDir = Vector2.Zero;

        if (direction == Vector2.Zero)
            return true;

        var curTime = _timing.CurTime;
        var sinceStroke = (curTime - swimmer.LastStroke).TotalSeconds;
        if (sinceStroke >= _strokeInterval)
        {
            swimmer.LastStroke = curTime;
            sinceStroke = 0;
            Dirty(uid, swimmer);
            _audio.PlayPredicted(StrokeSound, uid, uid);
        }

        if (sinceStroke < _strokeTime)
        {
            var speed = depth < _swimDepth ? MathF.Min(_strokeSpeed, _lyingStrokeSpeed) : _strokeSpeed;
            wishDir = direction.Normalized() * speed * (IsSinking(uid) ? _sinkStroke : 1f);
            accel = _strokeAccel;
        }

        return true;
    }

    /// <summary>
    ///     Depth this mob swims from right now. Lying down takes less of it, as long as the mob can move at all.
    /// </summary>
    public float GetSwimDepth(EntityUid uid)
    {
        return CanSwimLying(uid) ? MathF.Min(_lyingSwimDepth, _swimDepth) : _swimDepth;
    }

    /// <summary>
    ///     Whether the mob is swimming on its front, which puts its sprite sideways.
    /// </summary>
    public bool IsSwimmingLying(EntityUid uid, TransformComponent xform)
    {
        return CanSwimLying(uid) && !IsOnBottom(uid) && GetStandingDepth(uid, xform) >= MathF.Min(_lyingSwimDepth, _swimDepth);
    }

    private bool CanSwimLying(EntityUid uid)
    {
        return _lyingSwim && _standing.IsDown(uid) && _blocker.CanMove(uid);
    }

    /// <summary>
    ///     Water runs downhill: every neighbour pulls towards itself or pushes away by how much the two levels differ.
    ///     Walls and closed doors don't take part, a dry floor or the void beyond the hull is simply empty.
    ///     Under the sea that void is a full tile instead, so settled water along the hull doesn't run out into it.
    /// </summary>
    public Vector2 GetCurrent(EntityUid gridUid, MapGridComponent grid, Vector2i tile, float volume)
    {
        var current = Vector2.Zero;
        var underSea = _ambientQuery.HasComp(Transform(gridUid).MapUid);

        foreach (var offset in Neighbors)
        {
            var neighbor = tile + offset;
            var other = underSea && IsExposed(gridUid, grid, neighbor)
                ? FloodConstants.TileVolume.Float()
                : GetVolume(gridUid, grid, neighbor);
            var deeper = MathF.Max(volume, other);
            var difference = (volume - other) / deeper;
            if (MathF.Abs(difference) < _currentDeadzone || IsWaterBlocked(gridUid, grid, neighbor))
                continue;

            current += (Vector2) offset * difference * 0.5f;
        }

        return current;
    }

    private bool IsExposed(EntityUid gridUid, MapGridComponent grid, Vector2i tile)
    {
        return !_map.TryGetTileRef(gridUid, grid, tile, out var tileRef) || tileRef.Tile.IsEmpty || _turf.IsSpace(tileRef);
    }

    private bool IsWaterBlocked(EntityUid gridUid, MapGridComponent grid, Vector2i tile)
    {
        // Airlock layers don't include Impassable, so a shut door would read as an empty tile.
        var anchored = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, tile);
        while (anchored.MoveNext(out var ent))
        {
            if (_doorQuery.TryComp(ent, out var door) && door.State is not (DoorState.Open or DoorState.Opening))
                return true;
        }

        return _turf.IsTileBlocked(gridUid, tile, CollisionGroup.Impassable, grid);
    }

    /// <summary>
    ///     Active magboots hold you to the floor, so you wade along the bottom however deep it gets.
    /// </summary>
    /// <summary>
    ///     Ghosts, revenants, jaunts and the AI eye pass through liquid without touching it.
    /// </summary>
    public bool IsIntangible(EntityUid uid)
    {
        return HasComp<SpectralComponent>(uid) || HasComp<MovementIgnoreGravityComponent>(uid);
    }

    public bool IsOnBottom(EntityUid uid)
    {
        return _inventory.TryGetSlotEntity(uid, "shoes", out var boots) &&
               _magbootsQuery.HasComp(boots) &&
               _toggle.IsActivated(boots.Value);
    }

    public bool IsSinking(EntityUid uid)
    {
        return _inventory.TryGetSlotEntity(uid, "outerClothing", out var suit) && _sinkingQuery.HasComp(suit);
    }

    /// <summary>
    ///     Whether the mob's head is under. Swimming keeps it up, until the water gets too deep or the mob can't swim.
    /// </summary>
    public bool IsSubmerged(EntityUid uid, TransformComponent xform)
    {
        var depth = GetStandingDepth(uid, xform);
        if (depth >= _drownDepth)
            return true;

        return depth >= _swimDepth &&
               (IsOnBottom(uid) ||
                IsSinking(uid) ||
                _mobState.IsIncapacitated(uid) ||
                _staminaQuery.TryComp(uid, out var stamina) && stamina.Critical);
    }

    /// <summary>
    ///     Pressure in kPa that the water outside adds where this entity stands. Only tiles on a deep map that are nearly
    ///     full of mostly that water get any, a spilled tank doesn't crush anyone.
    /// </summary>
    public float GetAmbientPressure(EntityUid uid)
    {
        var xform = Transform(uid);
        if (xform.MapUid is not { } mapUid || !_ambientQuery.TryComp(mapUid, out var ambient))
            return 0f;

        if (IsInSea(xform))
            return ambient.Pressure;

        if (GetDepth(xform) < _pressureDepth ||
            !TryGetPuddle(xform, out var puddle) ||
            !_solution.ResolveSolution(puddle.Owner, puddle.Comp.SolutionName, ref puddle.Comp.Solution, out var solution) ||
            solution.Volume <= 0)
            return 0f;

        var share = (solution.GetTotalPrototypeQuantity(ambient.Reagent) / solution.Volume).Float();
        return share > ambient.MinShare ? ambient.Pressure : 0f;
    }

    public static float VolumeToDepth(float volume)
    {
        return MathF.Sqrt(Math.Clamp(volume / FloodConstants.TileVolume.Float(), 0f, 1f));
    }

    public float GetDepth(TransformComponent xform)
    {
        return VolumeToDepth(GetVolume(xform));
    }

    /// <summary>
    ///     How deep it is for this mob, which is less on top of a table. A flooded tile has no top left to stand on.
    /// </summary>
    public float GetStandingDepth(EntityUid uid, TransformComponent xform)
    {
        var depth = GetDepth(xform);
        if (depth <= 0f || depth >= _drownDepth ||
            !_climbingQuery.TryComp(uid, out var climbing) || !climbing.IsClimbing ||
            xform.GridUid is not { } gridUid || !_gridQuery.TryComp(gridUid, out var grid))
            return depth;

        return MathF.Max(depth - GetSurface(gridUid, grid, _map.TileIndicesFor(gridUid, grid, xform.Coordinates)), 0f);
    }

    /// <summary>
    ///     The height of the highest surface on this tile.
    /// </summary>
    public float GetSurface(EntityUid gridUid, MapGridComponent grid, Vector2i tile)
    {
        var surface = 0f;
        var anchored = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, tile);
        while (anchored.MoveNext(out var ent))
        {
            if (_maskQuery.TryComp(ent, out var mask))
                surface = MathF.Max(surface, mask.Surface);
        }

        return surface;
    }

    /// <summary>
    ///     Whether this sits on top of whatever surface shares its tile: loose objects do, mobs only once they've climbed up.
    /// </summary>
    public bool RestsOnSurface(EntityUid uid, TransformComponent xform)
    {
        if (_swimmerQuery.HasComp(uid))
            return _climbingQuery.TryComp(uid, out var climbing) && climbing.IsClimbing;

        return !xform.Anchored && !(_maskQuery.TryComp(uid, out var mask) && mask.Surface > 0f);
    }

    /// <summary>
    ///     The puddle the entity is standing in.
    /// </summary>
    public bool TryGetPuddle(TransformComponent xform, out Entity<PuddleComponent> puddle)
    {
        puddle = default;
        if (xform.GridUid is not { } gridUid || !IsLoose(xform, gridUid) || !_gridQuery.TryComp(gridUid, out var grid))
            return false;

        var anchored = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, _map.TileIndicesFor(gridUid, grid, xform.Coordinates));
        while (anchored.MoveNext(out var ent))
        {
            if (!_puddleQuery.TryComp(ent, out var comp))
                continue;

            puddle = (ent.Value, comp);
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Whether the entity is out in the sea itself, on an exposed tile or past the edge of the grid.
    /// </summary>
    public bool IsInSea(TransformComponent xform)
    {
        return TryGetSea(xform, out _);
    }

    /// <inheritdoc cref="IsInSea"/>
    public bool TryGetSea(TransformComponent xform, out FloodAmbientPressureComponent ambient)
    {
        ambient = default!;
        if (!_ambientQuery.TryComp(xform.MapUid, out var comp))
            return false;

        ambient = comp;
        if (xform.GridUid is not { } gridUid || !_gridQuery.TryComp(gridUid, out var grid))
            return xform.ParentUid == xform.MapUid;

        // The map's own ground is the sea floor, with the water all around.
        if (gridUid == xform.MapUid)
            return IsLoose(xform, gridUid);

        return IsLoose(xform, gridUid) && IsExposed(gridUid, grid, _map.TileIndicesFor(gridUid, grid, xform.Coordinates));
    }

    /// <summary>
    ///     The map's own ground under the entity, if it's water to swim in. That's out on the ground, or on a gap in a
    ///     grid floating over it. A lattice is no gap here, you stand on it.
    /// </summary>
    public bool TryGetSwimTile(TransformComponent xform, [NotNullWhen(true)] out ContentTileDefinition? tile)
    {
        tile = null;
        if (xform.MapUid is not { } mapUid ||
            !_gridQuery.TryComp(mapUid, out var ground) ||
            xform.GridUid is not { } gridUid ||
            !IsLoose(xform, gridUid))
            return false;

        if (gridUid != mapUid &&
            (!_gridQuery.TryComp(gridUid, out var grid) ||
             _map.TryGetTileRef(gridUid, grid, xform.Coordinates, out var own) && !own.Tile.IsEmpty))
            return false;

        if (!_map.TryGetTileRef(mapUid, ground, _transform.GetWorldPosition(xform), out var tileRef))
            return false;

        tile = _turf.GetContentTileDefinition(tileRef);
        return tile.SwimDepth > 0f;
    }

    /// <summary>
    ///     The colour of the liquid the entity is in, puddle or sea.
    /// </summary>
    public bool TryGetLiquidColor(TransformComponent xform, out Color color)
    {
        color = default;
        if (TryGetSea(xform, out var ambient))
        {
            if (!_proto.TryIndex(ambient.Reagent, out var reagent))
                return false;

            color = reagent.SubstanceColor;
            return true;
        }

        if (TryGetSwimTile(xform, out var tile))
        {
            if (!_proto.TryIndex(tile.SubmergedReagent, out var reagent))
                return false;

            color = reagent.SubstanceColor;
            return true;
        }

        if (!TryGetPuddle(xform, out var puddle))
            return false;

        color = GetColor(puddle);
        return true;
    }

    public Color GetColor(EntityUid puddle)
    {
        return _appearance.TryGetData<Color>(puddle, PuddleVisuals.SolutionColor, out var color) ? color : Color.White;
    }

    public float GetVolume(EntityUid puddle, PuddleComponent comp)
    {
        if (!_appearance.TryGetData<float>(puddle, PuddleVisuals.CurrentVolume, out var volume))
            return 0f;

        var overflow = comp.OverflowVolume;
        return volume * overflow.Float();
    }

    private float GetVolume(TransformComponent xform)
    {
        if (IsInSea(xform))
            return FloodConstants.TileVolume.Float();

        if (TryGetSwimTile(xform, out var tile))
            return tile.SwimDepth * tile.SwimDepth * FloodConstants.TileVolume.Float();

        if (xform.GridUid is not { } gridUid || !IsLoose(xform, gridUid) || !_gridQuery.TryComp(gridUid, out var grid))
            return 0f;

        return GetVolume(gridUid, grid, _map.TileIndicesFor(gridUid, grid, xform.Coordinates));
    }

    /// <summary>
    ///     Out in the open: not while held, in a locker and so on. Buckling parents you to the chair, which doesn't keep you dry.
    /// </summary>
    private bool IsLoose(TransformComponent xform, EntityUid gridUid)
    {
        return xform.ParentUid == gridUid ||
               _buckleQuery.TryComp(xform.Owner, out var buckle) && buckle.BuckledTo == xform.ParentUid &&
               Transform(xform.ParentUid).ParentUid == gridUid;
    }

    public float GetVolume(EntityUid gridUid, MapGridComponent grid, Vector2i tile)
    {
        var anchored = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, tile);
        while (anchored.MoveNext(out var ent))
        {
            if (_puddleQuery.TryComp(ent, out var puddle))
                return GetVolume(ent.Value, puddle);
        }

        return 0f;
    }
}
