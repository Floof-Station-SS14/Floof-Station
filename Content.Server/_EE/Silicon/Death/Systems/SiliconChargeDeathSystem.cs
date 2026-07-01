using Content.Server._Floof.Silicon.IPC;
using Content.Shared.Bed.Sleep;
using Content.Server.Humanoid;
using Content.Shared._Floof.Silicon.IPC;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Content.Shared.Power.Components;

namespace Content.Server._EE.Silicon.Death;

public sealed partial class SiliconDeathSystem : EntitySystem
{
    [Dependency] private SleepingSystem _sleep = default!;
    [Dependency] private SiliconChargeSystem _silicon = default!;
    [Dependency] private HumanoidProfileSystem _humanoidAppearanceSystem = default!;
    [Dependency] private HideableHumanoidLayersSystem _hideableHumanoidLayersSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SiliconDownOnDeadComponent, SiliconChargeStateUpdateEvent>(OnSiliconChargeStateUpdate);
    }

    private void OnSiliconChargeStateUpdate(EntityUid uid, SiliconDownOnDeadComponent siliconDeadComp, SiliconChargeStateUpdateEvent args)
    {
        if (!_silicon.TryGetSiliconBattery(uid, out var batteryComp))
        {
            SiliconDead(uid, siliconDeadComp, batteryComp, uid);
            return;
        }

        if (args.ChargePercent == 0 && siliconDeadComp.Dead)
            return;

        if (args.ChargePercent == 0 && !siliconDeadComp.Dead)
            SiliconDead(uid, siliconDeadComp, batteryComp, uid);
        else if (args.ChargePercent != 0 && siliconDeadComp.Dead)
            SiliconUnDead(uid, siliconDeadComp, batteryComp, uid);
    }

    private void SiliconDead(EntityUid uid, SiliconDownOnDeadComponent siliconDeadComp, BatteryComponent? batteryComp, EntityUid batteryUid)
    {
        var deadEvent = new SiliconChargeDyingEvent(uid, batteryComp, batteryUid);
        RaiseLocalEvent(uid, deadEvent);

        if (deadEvent.Cancelled)
            return;
        EnsureComp<SleepingComponent>(uid);
        //EntityManager.EnsureComponent<ForcedSleepingComponent>(uid); TODO

        if (TryComp<HumanoidProfileComponent>(uid, out HumanoidProfileComponent? humanoidAppearanceComponent))
        {
            var layers = HumanoidVisualLayersExtension.Sublayers(HumanoidVisualLayers.HeadSide);
            foreach (var layer in layers)
            {
                _hideableHumanoidLayersSystem.SetLayerOcclusion(uid, layer, true, SlotFlags.All); // TODO
            }

        }

        siliconDeadComp.Dead = true;

        RaiseLocalEvent(uid, new SiliconChargeDeathEvent(uid, batteryComp, batteryUid));
    }

    private void SiliconUnDead(EntityUid uid, SiliconDownOnDeadComponent siliconDeadComp, BatteryComponent? batteryComp, EntityUid batteryUid)
    {
        //RemComp<ForcedSleepingComponent>(uid); TODO
        _sleep.TryWaking(uid, true, null);

        siliconDeadComp.Dead = false;

        RaiseLocalEvent(uid, new SiliconChargeAliveEvent(uid, batteryComp, batteryUid));
    }
}

/// <summary>
///     A cancellable event raised when a Silicon is about to go down due to charge.
/// </summary>
/// <remarks>
///     This probably shouldn't be modified unless you intend to fill the Silicon's battery,
///     as otherwise it'll just be triggered again next frame.
/// </remarks>
public sealed class SiliconChargeDyingEvent : CancellableEntityEventArgs
{
    public EntityUid SiliconUid { get; }
    public BatteryComponent? BatteryComp { get; }
    public EntityUid BatteryUid { get; }

    public SiliconChargeDyingEvent(EntityUid siliconUid, BatteryComponent? batteryComp, EntityUid batteryUid)
    {
        SiliconUid = siliconUid;
        BatteryComp = batteryComp;
        BatteryUid = batteryUid;
    }
}

/// <summary>
///     An event raised after a Silicon has gone down due to charge.
/// </summary>
public sealed class SiliconChargeDeathEvent : EntityEventArgs
{
    public EntityUid SiliconUid { get; }
    public BatteryComponent? BatteryComp { get; }
    public EntityUid BatteryUid { get; }

    public SiliconChargeDeathEvent(EntityUid siliconUid, BatteryComponent? batteryComp, EntityUid batteryUid)
    {
        SiliconUid = siliconUid;
        BatteryComp = batteryComp;
        BatteryUid = batteryUid;
    }
}

/// <summary>
///     An event raised after a Silicon has reawoken due to an increase in charge.
/// </summary>
public sealed class SiliconChargeAliveEvent : EntityEventArgs
{
    public EntityUid SiliconUid { get; }
    public BatteryComponent? BatteryComp { get; }
    public EntityUid BatteryUid { get; }

    public SiliconChargeAliveEvent(EntityUid siliconUid, BatteryComponent? batteryComp, EntityUid batteryUid)
    {
        SiliconUid = siliconUid;
        BatteryComp = batteryComp;
        BatteryUid = batteryUid;
    }
}
