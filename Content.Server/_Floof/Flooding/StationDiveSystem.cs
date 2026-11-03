using System.Linq;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Chat.Systems;
using Content.Server.Communications;
using Content.Server.Fluids.EntitySystems;
using Content.Server.GameTicking;
using Content.Server.Parallax;
using Content.Server.RoundEnd;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Components;
using Content.Server.Station.Events;
using Content.Shared._Floof.CCVars;
using Content.Shared._Floof.Flooding;
using Content.Shared.Atmos;
using Content.Shared.Camera;
using Content.Shared.Fluids.Components;
using Content.Shared.GameTicking;
using Content.Shared.Gravity;
using Content.Shared.Light.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Parallax;
using Content.Shared.Popups;
using Content.Shared.Station.Components;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Floof.Flooding;

/// <summary>
///     Runs a station's dive: warns the crew, moves the station and anything docked to it to a deep map, sinks it, and
///     brings it back up when the round end countdown runs out. Nothing really moves besides the one snap between maps.
/// </summary>
public sealed partial class StationDiveSystem : EntitySystem
{
    [Dependency] private AtmosphereSystem _atmos = default!;
    [Dependency] private BiomeSystem _biome = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private CommunicationsConsoleSystem _comms = default!;
    [Dependency] private DockingSystem _dock = default!;
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private PuddleSystem _puddle = default!;
    [Dependency] private RoundEndSystem _roundEnd = default!;
    [Dependency] private SharedCameraRecoilSystem _recoil = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _xform = default!;

    private const float SurfacePressure = 101.325f;
    private const float DeepPressure = 600f;

    /// <summary>
    ///     Share of the full depth where the pressure stops rising.
    /// </summary>
    private const float FullPressureDepth = 0.4f;

    /// <summary>
    ///     Where the floor comes into view, same as floorFrom in flood_deep.swsl.
    /// </summary>
    private const float LandingDepth = 0.83f;

    private const int LandingShakes = 10;
    private const float LandingShakeStrength = 0.6f;
    private static readonly TimeSpan LandingShakeInterval = TimeSpan.FromSeconds(0.15);

    private const float UpdateInterval = 0.5f;

    private static readonly TimeSpan FirstWarning = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan LastWarning = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan LiftOffWarning = TimeSpan.FromSeconds(60);

    private const string ResurfaceText = "station-dive-resurface-announcement";
    private const string AbortText = "station-dive-abort-announcement";

    private float _accumulator;
    private readonly HashSet<EntityUid> _savedGrids = new();

    public bool Active => EntityQuery<StationDiveComponent>().Any();

    public bool CanResurface
    {
        get
        {
            var query = EntityQueryEnumerator<StationDiveComponent>();
            return query.MoveNext(out var dive) && dive.State == StationDiveState.Deep;
        }
    }

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StationDiveComponent, StationPostInitEvent>(OnStationPostInit);
        SubscribeLocalEvent<RoundEndSystemChangedEvent>(OnRoundEndChanged);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => _savedGrids.Clear());
    }

    /// <summary>
    ///     Made it back to the surface aboard the station or a shuttle docked to it.
    /// </summary>
    public bool IsSaved(EntityUid body)
    {
        return Transform(body).GridUid is { } grid && _savedGrids.Contains(grid);
    }

    private void OnStationPostInit(Entity<StationDiveComponent> ent, ref StationPostInitEvent args)
    {
        if (!TryComp<StationDataComponent>(ent, out var data) ||
            !TryComp(data.Grids.FirstOrDefault(), out TransformComponent? stationXform) ||
            stationXform.MapUid is not { } surface)
            return;

        _biome.EnsurePlanet(surface, _proto.Index(ent.Comp.SurfaceBiome), mapLight: ent.Comp.SurfaceLight);
        RemComp<LightCycleComponent>(surface);
        RemComp<SunShadowCycleComponent>(surface);

        // The map isn't initialized yet, so the cycle never learned the color it just restored on removal.
        var surfaceLight = EnsureComp<MapLightComponent>(surface);
        surfaceLight.AmbientLightColor = ent.Comp.SurfaceLight;
        Dirty(surface, surfaceLight);

        ent.Comp.SurfaceMap = surface;

        var deep = _map.CreateMap();
        var depth = EnsureComp<FloodDepthComponent>(deep);
        depth.StartTime = _timing.CurTime;
        depth.SurfaceLight = ent.Comp.SurfaceLight;
        Dirty(deep, depth);
        EnsureComp<MapLightComponent>(deep);
        var ambient = EnsureComp<FloodAmbientPressureComponent>(deep);
        ambient.Pressure = SurfacePressure;
        Dirty(deep, ambient);
        var parallax = EnsureComp<ParallaxComponent>(deep);
        parallax.Parallax = ent.Comp.DeepParallax;
        Dirty(deep, parallax);
        var gravity = EnsureComp<GravityComponent>(deep);
        gravity.Enabled = true;
        gravity.Inherent = true;
        Dirty(deep, gravity);
        ent.Comp.DeepMap = deep;

        var seabed = _map.CreateMap();
        _biome.EnsurePlanet(seabed, _proto.Index(ent.Comp.SeabedBiome), mapLight: Color.Black);
        RemComp<LightCycleComponent>(seabed);
        RemComp<SunShadowCycleComponent>(seabed);
        _atmos.SetMapAtmosphere(seabed, true, GasMixture.SpaceGas);
        var seabedDepth = EnsureComp<FloodDepthComponent>(seabed);
        seabedDepth.StartDepth = 1f;
        seabedDepth.StartTime = _timing.CurTime;
        seabedDepth.Seabed = true;
        Dirty(seabed, seabedDepth);
        var seabedAmbient = EnsureComp<FloodAmbientPressureComponent>(seabed);
        seabedAmbient.Pressure = DeepPressure;
        Dirty(seabed, seabedAmbient);
        ent.Comp.SeabedMap = seabed;

        var grids = GetGrids(ent);
        ent.Comp.SurfaceBounds = GetBounds(grids).Enlarged(ent.Comp.SurfaceRoam);

        _roundEnd.CountdownOverride = new RoundEndCountdownOverride(
            TimeSpan.FromSeconds(_cfg.GetCVar(CCVars.FloodResurfaceTime)),
            ResurfaceText,
            AbortText,
            "station-dive-sender",
            () => FinishResurface(ent));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var shaking = EntityQueryEnumerator<StationDiveComponent>();
        while (shaking.MoveNext(out var uid, out var dive))
        {
            if (dive.ShakesLeft > 0 && _timing.CurTime >= dive.NextShake)
                ShakeLanding((uid, dive));
        }

        _accumulator += frameTime;
        if (_accumulator < UpdateInterval)
            return;
        _accumulator -= UpdateInterval;

        if (_ticker.RunLevel != GameRunLevel.InRound)
            return;

        var query = EntityQueryEnumerator<StationDiveComponent>();
        while (query.MoveNext(out var uid, out var dive))
        {
            if (dive.State == StationDiveState.Surface)
            {
                UpdateCountdown((uid, dive));
                KeepInBounds(dive);
            }
            else if (dive.State == StationDiveState.Deep)
            {
                UpdatePressure(dive);
                UpdateLanding((uid, dive));
                UpdateLiftOff((uid, dive));
                KeepInBounds(dive);
            }
        }
    }

    private void UpdateLanding(Entity<StationDiveComponent> ent)
    {
        if (!TryComp<FloodDepthComponent>(ent.Comp.DeepMap, out var depth))
            return;

        var now = depth.GetDepth(_timing.CurTime);

        // Each is reset once back above it, so turning around close to the bottom doesn't repeat the warning.
        if (now < LandingDepth)
            ent.Comp.WarnedLanding = false;
        else if (!ent.Comp.WarnedLanding && depth.Rate > 0f)
        {
            ent.Comp.WarnedLanding = true;
            _chat.DispatchGlobalAnnouncement(Loc.GetString("station-dive-landing"),
                Loc.GetString("station-dive-sender"),
                colorOverride: Color.Gold);
        }

        if (now < 1f)
            ent.Comp.Landed = false;
        else if (!ent.Comp.Landed)
        {
            ent.Comp.Landed = true;
            ent.Comp.ShakesLeft = LandingShakes;
            ent.Comp.NextShake = _timing.CurTime;
            Land(ent);
        }
    }

    /// <summary>
    ///     Touchdown, the swap to the real floor. Everything in the water comes along, not just the station, as nothing
    ///     can be left on the map that's only for travelling.
    /// </summary>
    private void Land(Entity<StationDiveComponent> ent)
    {
        var grids = GetGrids(ent);
        var loose = new List<EntityUid>();
        var children = Transform(ent.Comp.DeepMap).ChildEnumerator;
        while (children.MoveNext(out var child))
        {
            if (HasComp<MapGridComponent>(child))
                grids.Add(child);
            else
                loose.Add(child);
        }

        MoveGrids(grids, ent.Comp.SeabedMap);

        foreach (var uid in loose)
        {
            var xform = Transform(uid);
            var (position, rotation) = _xform.GetWorldPositionRotation(xform);
            _xform.SetCoordinates(uid, xform, new EntityCoordinates(ent.Comp.SeabedMap, position), rotation);
        }

        ent.Comp.OnSeabed = true;

        // Down here a resurface call only starts the lift-off warning. The station isn't rising until it's over.
        if (_roundEnd.CountdownOverride is { } countdown)
            _roundEnd.CountdownOverride = countdown with { CallText = null, RecallText = "station-dive-liftoff-abort-announcement" };
    }

    private void UpdateLiftOff(Entity<StationDiveComponent> ent)
    {
        if (ent.Comp.LiftOffAt is not { } liftOff || _timing.CurTime < liftOff)
            return;

        ent.Comp.LiftOffAt = null;
        ent.Comp.OnSeabed = false;

        // Only what's aboard goes. Whoever is still out there stays down there.
        MoveGrids(GetGrids(ent), ent.Comp.DeepMap);

        if (_roundEnd.CountdownOverride is { } countdown)
            _roundEnd.CountdownOverride = countdown with { CallText = ResurfaceText, RecallText = AbortText };

        if (_roundEnd.ShuttleTimeLeft is not { } left)
            return;

        SetRate(ent, -1f / MathF.Max((float) left.TotalSeconds, 1f));

        var minutes = left.TotalSeconds >= 60;
        var time = (int) Math.Ceiling(minutes ? left.TotalMinutes : left.TotalSeconds);
        _chat.DispatchGlobalAnnouncement(
            Loc.GetString(ResurfaceText,
                ("time", time),
                ("units", Loc.GetString(minutes ? "eta-units-minutes" : "eta-units-seconds"))),
            Loc.GetString("station-dive-sender"),
            colorOverride: Color.Gold);
    }

    /// <summary>
    ///     One hard jolt that dies down, for everyone aboard.
    /// </summary>
    private void ShakeLanding(Entity<StationDiveComponent> ent)
    {
        var strength = LandingShakeStrength * ent.Comp.ShakesLeft / LandingShakes;
        ent.Comp.ShakesLeft--;
        ent.Comp.NextShake = _timing.CurTime + LandingShakeInterval;

        var grids = GetGrids(ent);
        foreach (var session in _player.Sessions)
        {
            if (session.AttachedEntity is { } player &&
                Transform(player).GridUid is { } grid &&
                grids.Contains(grid))
            {
                _recoil.KickCamera(player, _random.NextAngle().ToVec() * strength);
            }
        }
    }

    private void UpdateCountdown(Entity<StationDiveComponent> ent)
    {
        // The round's already on its way out. There's no point in going down now.
        if (_roundEnd.IsRoundEndRequested())
            return;

        var left = TimeSpan.FromSeconds(_cfg.GetCVar(CCVars.FloodDiveDelay)) - _ticker.RoundDuration();

        if (!ent.Comp.WarnedFirst && left <= FirstWarning)
        {
            ent.Comp.WarnedFirst = true;
            _chat.DispatchGlobalAnnouncement(Loc.GetString("station-dive-warning-first"),
                Loc.GetString("station-dive-sender"),
                colorOverride: Color.Gold);
        }

        if (!ent.Comp.WarnedLast && left <= LastWarning)
        {
            ent.Comp.WarnedLast = true;
            _chat.DispatchGlobalAnnouncement(Loc.GetString("station-dive-warning-last"),
                Loc.GetString("station-dive-sender"),
                colorOverride: Color.Red);
        }

        if (left <= TimeSpan.Zero)
            Dive(ent);
    }

    private void Dive(Entity<StationDiveComponent> ent)
    {
        var grids = GetGrids(ent);
        MoveGrids(grids, ent.Comp.DeepMap);

        ent.Comp.State = StationDiveState.Deep;
        SetRate(ent, 1f / _cfg.GetCVar(CCVars.FloodDiveTime));

        _chat.DispatchGlobalAnnouncement(Loc.GetString("station-dive-started"),
            Loc.GetString("station-dive-sender"),
            colorOverride: Color.Gold);

        _comms.UpdateCommsConsoleInterface();
    }

    private void FinishResurface(Entity<StationDiveComponent> ent)
    {
        if (TerminatingOrDeleted(ent))
            return;

        var grids = GetGrids(ent);
        if (ent.Comp.State == StationDiveState.Deep)
            MoveGrids(grids, ent.Comp.SurfaceMap);

        ent.Comp.State = StationDiveState.Surfaced;
        _savedGrids.UnionWith(grids);

        _chat.DispatchGlobalAnnouncement(Loc.GetString("station-dive-surfaced"),
            Loc.GetString("station-dive-sender"),
            colorOverride: Color.Gold);

        _roundEnd.EndRound();
    }

    private void OnRoundEndChanged(RoundEndSystemChangedEvent args)
    {
        var query = EntityQueryEnumerator<StationDiveComponent>();
        while (query.MoveNext(out var uid, out var dive))
        {
            if (dive.State != StationDiveState.Deep)
                continue;

            var resurfacing = _roundEnd.ShuttleTimeLeft is not null;
            if (resurfacing == dive.Resurfacing)
                continue;

            dive.Resurfacing = resurfacing;

            if (dive.OnSeabed)
            {
                dive.LiftOffAt = null;
                if (!resurfacing || _roundEnd.ShuttleTimeLeft is not { } timeLeft)
                    continue;

                // Half the countdown at most, for when it's been cut short.
                var wait = TimeSpan.FromSeconds(Math.Min(LiftOffWarning.TotalSeconds, timeLeft.TotalSeconds / 2));
                dive.LiftOffAt = _timing.CurTime + wait;
                _chat.DispatchGlobalAnnouncement(
                    Loc.GetString("station-dive-liftoff-warning", ("seconds", (int) wait.TotalSeconds)),
                    Loc.GetString("station-dive-sender"),
                    colorOverride: Color.Red);
                continue;
            }

            if (resurfacing && _roundEnd.ShuttleTimeLeft is { } left && TryComp<FloodDepthComponent>(dive.DeepMap, out var depth))
                SetRate((uid, dive), -depth.GetDepth(_timing.CurTime) / MathF.Max((float) left.TotalSeconds, 1f));
            else
                SetRate((uid, dive), 1f / _cfg.GetCVar(CCVars.FloodDiveTime));
        }
    }

    private void SetRate(Entity<StationDiveComponent> ent, float rate)
    {
        if (!TryComp<FloodDepthComponent>(ent.Comp.DeepMap, out var depth))
            return;

        depth.StartTravel = depth.GetTravel(_timing.CurTime);
        depth.StartDepth = depth.GetDepth(_timing.CurTime);
        depth.StartTime = _timing.CurTime;
        depth.Rate = rate;
        Dirty(ent.Comp.DeepMap, depth);
    }

    private void UpdatePressure(StationDiveComponent dive)
    {
        if (!TryComp<FloodDepthComponent>(dive.DeepMap, out var depth) ||
            !TryComp<FloodAmbientPressureComponent>(dive.DeepMap, out var ambient))
            return;

        var share = MathF.Min(depth.GetDepth(_timing.CurTime) / FullPressureDepth, 1f);
        var pressure = SurfacePressure + (DeepPressure - SurfacePressure) * share;
        if (MathHelper.CloseTo(pressure, ambient.Pressure))
            return;

        ambient.Pressure = pressure;
        Dirty(dive.DeepMap, ambient);
    }

    private void KeepInBounds(StationDiveComponent dive)
    {
        foreach (var session in _player.Sessions)
        {
            if (session.AttachedEntity is not { } player ||
                !HasComp<MobStateComponent>(player))
                continue;

            var xform = Transform(player);
            if (xform.MapUid != dive.SurfaceMap && xform.MapUid != dive.SeabedMap || xform.GridUid != xform.MapUid)
                continue;

            var position = _xform.GetWorldPosition(xform);
            if (dive.SurfaceBounds.Contains(position))
                continue;

            _xform.SetWorldPosition(player, dive.SurfaceBounds.ClosestPoint(position));
            _popup.PopupEntity(Loc.GetString("station-dive-too-far"), player, player, PopupType.MediumCaution);
        }
    }

    /// <summary>
    ///     The station's own grids, plus whatever is docked to them, directly or through another shuttle.
    /// </summary>
    private HashSet<EntityUid> GetGrids(Entity<StationDiveComponent> ent)
    {
        var grids = new HashSet<EntityUid>();
        if (!TryComp<StationDataComponent>(ent, out var data))
            return grids;

        var open = new Queue<EntityUid>(data.Grids);
        while (open.TryDequeue(out var grid))
        {
            if (!grids.Add(grid))
                continue;

            foreach (var dock in _dock.GetDocks(grid))
            {
                if (dock.Comp.DockedWith is { } other && Transform(other).GridUid is { } otherGrid)
                    open.Enqueue(otherGrid);
            }
        }

        return grids;
    }

    private void MoveGrids(HashSet<EntityUid> grids, EntityUid map)
    {
        // The joints holding docks together don't survive a map change, so they're made again after.
        var pairs = new List<(Entity<DockingComponent> A, Entity<DockingComponent> B)>();
        foreach (var grid in grids)
        {
            foreach (var dock in _dock.GetDocks(grid))
            {
                if (dock.Comp.DockedWith is { } other &&
                    TryComp<DockingComponent>(other, out var otherDock) &&
                    !pairs.Any(pair => pair.A.Owner == other && pair.B.Owner == dock.Owner))
                {
                    pairs.Add((dock, (other, otherDock)));
                }
            }
        }

        foreach (var (dock, _) in pairs)
        {
            _dock.Undock(dock);
        }

        foreach (var grid in grids)
        {
            var xform = Transform(grid);
            var (position, rotation) = _xform.GetWorldPositionRotation(xform);
            _xform.SetCoordinates(grid, xform, new EntityCoordinates(map, position), rotation);
        }

        foreach (var (a, b) in pairs)
        {
            _dock.Dock(a, b);
        }

        // Settled puddles don't notice that the sea outside changed.
        var puddles = EntityQueryEnumerator<PuddleComponent, TransformComponent>();
        while (puddles.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.GridUid is { } grid && grids.Contains(grid))
                _puddle.WakePuddle(uid);
        }
    }

    private Box2 GetBounds(HashSet<EntityUid> grids)
    {
        Box2? bounds = null;
        foreach (var grid in grids)
        {
            if (!TryComp<MapGridComponent>(grid, out var comp))
                continue;

            var box = _xform.GetWorldMatrix(grid).TransformBox(comp.LocalAABB);
            bounds = bounds?.Union(box) ?? box;
        }

        return bounds ?? new Box2();
    }
}
