using Content.Shared._EE.Silicon.Components;
using Content.Shared.PowerCell.Components;
using Content.Shared.Silicons.Borgs.Components;
using Robust.Shared.Player;

namespace Content.Shared._Floof.Silicon.IPC;

public sealed partial class SiliconSystem
{
    // How often to update the battery alert.
    // Also gets updated instantly when switching bodies or a battery is inserted or removed.
    private static readonly TimeSpan AlertUpdateDelay = TimeSpan.FromSeconds(0.5f);

    public void InitializeBattery()
    {
        SubscribeLocalEvent<SiliconComponent, LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<SiliconComponent, LocalPlayerDetachedEvent>(OnPlayerDetached);
    }

    private void OnPlayerAttached(Entity<SiliconComponent> ent, ref LocalPlayerAttachedEvent args)
    {
        UpdateBatteryAlert((ent.Owner, ent.Comp, null));
    }

    private void OnPlayerDetached(Entity<SiliconComponent> ent, ref LocalPlayerDetachedEvent args)
    {
        // Remove all borg related alerts.
        _alerts.ClearAlert(ent.Owner, ent.Comp.BatteryAlert);
        _alerts.ClearAlert(ent.Owner, ent.Comp.NoBatteryAlert);
    }

    private void UpdateBatteryAlert(Entity<SiliconComponent, PowerCellSlotComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp2, false))
            return;

        if (!_powerCell.TryGetBatteryFromSlot((ent.Owner, ent.Comp2), out var battery))
        {
            _alerts.ShowAlert(ent.Owner, ent.Comp1.NoBatteryAlert);
            return;
        }

        // Alert levels from 0 to 10.
        var chargeLevel = (short)MathF.Round(_battery.GetChargeLevel(battery.Value.AsNullable()) * 10f);

        // we make sure 0 only shows if they have absolutely no battery.
        // also account for floating point imprecision
        if (chargeLevel == 0 && _powerCell.HasDrawCharge((ent.Owner, null, ent.Comp2)))
        {
            chargeLevel = 1;
        }

        _alerts.ShowAlert(ent.Owner, ent.Comp1.BatteryAlert, chargeLevel);
    }

    // Periodically update the charge indicator.
    // We do this with a client-side alert so that we don't have to network the charge level.
    public void UpdateBattery()
    {
        if (_player.LocalEntity is not { } localPlayer)
            return;

        if (!_chassisQuery.TryComp(localPlayer, out var chassis) || !_slotQuery.TryComp(localPlayer, out var slot))
            return;

        UpdateBatteryAlert((localPlayer, chassis, slot));
    }
}
