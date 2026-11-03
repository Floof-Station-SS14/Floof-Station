using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Floof.Flooding;

/// <summary>
///     On a map sitting deep underwater, so everything on it shares the depth: the station, shuttles docked to it and so
///     on. Anything open to the map takes on water, and flooded tiles equalise with the water outside, so whoever stands
///     in them feels this much pressure on top of the air's. Dry compartments stay as they are.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FloodAmbientPressureComponent : Component
{
    /// <summary>
    ///     Pressure of the water outside in kPa. 100 kPa is about 10 meters, and barotrauma starts hurting at 550.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Pressure = 600f;

    /// <summary>
    ///     What the water outside is.
    /// </summary>
    [DataField, AutoNetworkedField]
    public ProtoId<ReagentPrototype> Reagent = "Seawater";

    /// <summary>
    ///     Share of a puddle that has to be the water outside before it carries the pressure.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float MinShare = 0.5f;

    /// <summary>
    ///     How fast the water comes in through each open side of a tile.
    /// </summary>
    [DataField]
    public FixedPoint2 InflowPerSecond = FixedPoint2.New(30000);
}
