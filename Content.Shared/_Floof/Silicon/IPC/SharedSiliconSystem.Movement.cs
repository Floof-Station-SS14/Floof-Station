using Content.Shared._EE.Silicon.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Power.EntitySystems;

namespace Content.Shared._Floof.Silicon.IPC;


public abstract partial class SharedSiliconSystem
{
    [Dependency] protected MovementSpeedModifierSystem _movement = default!;
    [Dependency] protected SharedBatterySystem _battery = default!;
    public void InitializeMovement()
    {
        SubscribeLocalEvent<SiliconComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovespeed);


    }

    private int GetChargeState(EntityUid uid, SiliconComponent component)
    {
        if (!component.BatteryPowered)
            return 100;

        if (!_powerCell.TryGetBatteryFromEntityOrSlot(uid, out var battery))
            return 0;

        return (int)(_battery.GetChargeLevel(battery.Value.AsNullable())*100f);
    }
    private int GetMovementModifierState(EntityUid uid, SiliconComponent component)
    {
        var closest = 0;

        int chargeState = GetChargeState(uid, component);

        foreach (var state in component.SpeedModifierThresholds)
            if (chargeState >= state.Key && state.Key > closest)
                closest = state.Key;

        return closest;
    }
    private void OnRefreshMovespeed(EntityUid uid, SiliconComponent component, RefreshMovementSpeedModifiersEvent args)
    {
        if (!component.BatteryPowered)
            return;

        var closest = 0;

        var state = GetMovementModifierState(uid, component);

        var speedMod = component.SpeedModifierThresholds[state];

        args.ModifySpeed(speedMod, speedMod);
    }

}
