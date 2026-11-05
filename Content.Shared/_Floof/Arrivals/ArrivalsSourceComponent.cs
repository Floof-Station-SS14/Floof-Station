// moved from space-wizards/space-station-14 Content.Shared/Shuttles/Components/ArrivalsSourceComponent.cs (MIT, see LICENSE-MIT.TXT).
// SPDX-License-Identifier: MIT AND AGPL-3.0-or-later

using Content.Shared.Shuttles.Systems;

namespace Content.Shared.Shuttles.Components;

/// <summary>
/// Added to a designated arrivals station for players to spawn at, if enabled.
/// </summary>
[RegisterComponent, Access(typeof(ArrivalsSystem))]
public sealed partial class ArrivalsSourceComponent : Component
{
    /// <summary>
    /// The station this terminal serves, set when it was loaded from <see cref="StationArrivalsComponent.ArrivalsMap"/>.
    /// </summary>
    [ViewVariables]
    public EntityUid? Station;
}
