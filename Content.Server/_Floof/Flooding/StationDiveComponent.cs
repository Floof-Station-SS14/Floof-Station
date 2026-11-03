using Content.Shared.Parallax.Biomes;
using Robust.Shared.Prototypes;

namespace Content.Server._Floof.Flooding;

public enum StationDiveState : byte
{
    Surface,
    Deep,
    Surfaced,
}

/// <summary>
///     A station that starts on the ocean's surface, dives during the round and has to come back up to end it.
/// </summary>
[RegisterComponent, Access(typeof(StationDiveSystem))]
public sealed partial class StationDiveComponent : Component
{
    [DataField(required: true)]
    public ProtoId<BiomeTemplatePrototype> SurfaceBiome;

    [DataField(required: true)]
    public ProtoId<BiomeTemplatePrototype> SeabedBiome;

    [DataField]
    public string DeepParallax = "MantaDeep";

    /// <summary>
    ///     How far past the station's bounds people can wander on the surface.
    /// </summary>
    [DataField]
    public float SurfaceRoam = 40f;

    [DataField]
    public Color SurfaceLight = Color.White;

    [ViewVariables]
    public StationDiveState State;

    [ViewVariables]
    public EntityUid SurfaceMap;

    [ViewVariables]
    public EntityUid DeepMap;

    [ViewVariables]
    public EntityUid SeabedMap;

    [ViewVariables]
    public Box2 SurfaceBounds;

    public bool WarnedFirst;
    public bool WarnedLast;
    public bool Resurfacing;
    public bool WarnedLanding;
    public bool Landed;
    public bool OnSeabed;
    public TimeSpan? LiftOffAt;
    public int ShakesLeft;
    public TimeSpan NextShake;
}
