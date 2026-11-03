using Robust.Shared.GameStates;

namespace Content.Shared._Floof.Flooding;

/// <summary>
///     How deep this map is, 0 at the surface and 1 at the bottom. Only changes of direction are networked: everyone
///     works the current value out from these.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FloodDepthComponent : Component
{
    [DataField, AutoNetworkedField]
    public float StartDepth;

    [DataField, AutoNetworkedField]
    public TimeSpan StartTime;

    /// <summary>
    ///     Depth gained per second. Negative on the way up.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Rate;

    /// <summary>
    ///     The depth of the map in meters at 1. Flavour, as nothing is really moving.
    /// </summary>
    [DataField]
    public float Meters = 600f;

    /// <summary>
    ///     The depth at which no daylight is left.
    /// </summary>
    [DataField]
    public float DarkAt = 0.5f;

    /// <summary>
    ///     The depth down to which the sea still looks like the surface did, so moving maps doesn't show.
    /// </summary>
    [DataField]
    public float SurfaceHoldTo = 0.004f;

    /// <summary>
    ///     The depth by which the surface has zoomed past and the water has climbed everything outside.
    /// </summary>
    [DataField]
    public float SubmergedFrom = 0.018f;

    /// <summary>
    ///     The map's light at the surface. It fades along with the water.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Color SurfaceLight = Color.Black;

    /// <summary>
    ///     How far the water had gone past the station at <see cref="StartTime"/>, in tiles.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float StartTravel;

    /// <summary>
    ///     Tiles of water passing by per share of depth at full speed. Just a look, the station stays where it is.
    /// </summary>
    [DataField]
    public float TravelSpeed = 450f;

    /// <summary>
    ///     The depth by which the station is up to full speed.
    /// </summary>
    [DataField]
    public float TravelFullFrom = 0.05f;

    /// <summary>
    ///     The depth at which it starts slowing down, to stop at the bottom.
    /// </summary>
    [DataField]
    public float TravelSlowFrom = 0.9f;

    /// <summary>
    ///     The map the station lands on, resting at full depth. The dive's own progress is on the map it travels through.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Seabed;

    public float GetDepth(TimeSpan now)
    {
        return Math.Clamp(StartDepth + Rate * (float) (now - StartTime).TotalSeconds, 0f, 1f);
    }

    /// <summary>
    ///     How far the water has gone past the station. Only ever grows, so it keeps going forward on the way up too.
    /// </summary>
    public float GetTravel(TimeSpan now)
    {
        return StartTravel + MathF.Abs(TravelAt(GetDepth(now)) - TravelAt(StartDepth)) * TravelSpeed;
    }

    /// <summary>
    ///     The speed ramps up and down in straight lines, so this is the area under them up to this depth.
    /// </summary>
    private float TravelAt(float depth)
    {
        if (depth <= TravelFullFrom)
            return depth * depth / (2f * TravelFullFrom);

        var cruised = TravelFullFrom / 2f + MathF.Min(depth, TravelSlowFrom) - TravelFullFrom;
        if (depth <= TravelSlowFrom)
            return cruised;

        var slowing = 1f - TravelSlowFrom;
        var left = 1f - depth;
        return cruised + (slowing * slowing - left * left) / (2f * slowing);
    }

    /// <summary>
    ///     How much daylight reaches this far down, 1 at the surface and 0 in the dark.
    /// </summary>
    public float GetBrightness(TimeSpan now)
    {
        var share = Math.Clamp(GetDepth(now) / DarkAt, 0f, 1f);
        var light = 1f - share * share * (3f - 2f * share);
        return light * light;
    }

    /// <summary>
    ///     How far the station has gone under, 0 while it still looks like the surface and 1 once the water covers it all.
    /// </summary>
    public float GetSubmerged(TimeSpan now)
    {
        var share = Math.Clamp((GetDepth(now) - SurfaceHoldTo) / (SubmergedFrom - SurfaceHoldTo), 0f, 1f);
        return share * share * (3f - 2f * share);
    }
}
