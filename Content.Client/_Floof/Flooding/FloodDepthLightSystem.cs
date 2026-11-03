using Content.Shared._Floof.Flooding;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Client._Floof.Flooding;

/// <summary>
///     Dims a map's light along with its water. Everyone works depth out on their own, so this isn't networked.
/// </summary>
public sealed partial class FloodDepthLightSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var query = EntityQueryEnumerator<FloodDepthComponent, MapLightComponent>();
        while (query.MoveNext(out var depth, out var light))
        {
            var brightness = depth.GetBrightness(_timing.CurTime);
            var color = depth.SurfaceLight;
            light.AmbientLightColor = new Color(color.R * brightness, color.G * brightness, color.B * brightness);
        }
    }
}
