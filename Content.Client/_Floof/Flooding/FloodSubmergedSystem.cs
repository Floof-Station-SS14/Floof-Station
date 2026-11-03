using Content.Client.Fluids;
using Content.Shared._Floof.Flooding;
using Robust.Client.Graphics;
using Robust.Client.Player;

namespace Content.Client._Floof.Flooding;

/// <summary>
///     With the local player's head under, the view takes on a deep shade of the liquid and wavers,
///     but the liquid itself turns clear, so diving is how you find what's down there.
/// </summary>
public sealed partial class FloodSubmergedSystem : EntitySystem
{
    [Dependency] private FloodWallSystem _floodWall = default!;
    [Dependency] private FloodWaterSystem _water = default!;
    [Dependency] private IOverlayManager _overlay = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private PuddleSystem _puddle = default!;

    private const float FadeSeconds = 0.3f;
    private const float Clarity = 0.35f;
    private const float HueSeconds = 0.5f;

    private FloodSubmergedOverlay _view = default!;
    private bool _submerged;

    public override void Initialize()
    {
        base.Initialize();

        _view = new FloodSubmergedOverlay();
        _overlay.AddOverlay(_view);
        _overlay.AddOverlay(new FloodCausticsOverlay());
        _overlay.AddOverlay(new FloodFogOverlay());
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _overlay.RemoveOverlay(_view);
        _overlay.RemoveOverlay<FloodCausticsOverlay>();
        _overlay.RemoveOverlay<FloodFogOverlay>();
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var submerged = false;
        if (_player.LocalEntity is { } uid && TryComp(uid, out TransformComponent? xform) &&
            HasComp<FloodSwimmerComponent>(uid) && _water.IsSubmerged(uid, xform))
        {
            submerged = true;
            if (_water.TryGetLiquidColor(xform, out var liquid))
                UpdateHue(liquid, frameTime);
        }

        _view.Strength = Math.Clamp(_view.Strength + (submerged ? frameTime : -frameTime) / FadeSeconds, 0f, 1f);
        _view.Tint = _floodWall.SubmergedTint;
        _floodWall.SubmergedView = _view.Strength;

        if (submerged == _submerged)
            return;

        _submerged = submerged;
        _puddle.SetFloodClarity(submerged ? Clarity : 1f);
    }

    private void UpdateHue(Color liquid, float frameTime)
    {
        var brightest = MathF.Max(liquid.R, MathF.Max(liquid.G, liquid.B));
        if (brightest <= 0f)
            return;

        var hue = new Color(liquid.R / brightest, liquid.G / brightest, liquid.B / brightest);

        // Diving in shows the new liquid straight away, swimming into another one blends over.
        var blend = _view.Strength <= 0f ? 1f : 1f - MathF.Exp(-frameTime / HueSeconds);
        _floodWall.SubmergedHue = Color.InterpolateBetween(_floodWall.SubmergedHue, hue, blend);
    }
}
