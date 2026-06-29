using Robust.Shared.Random;
using Content.Shared._EE.Silicon.Components;
using Content.Server.Power.Components;
using Content.Shared.Mobs.Systems;
using Content.Server.Temperature.Components;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Popups;
using Content.Shared.Popups;
using Content.Shared._EE.Silicon.Systems;
using Content.Shared.Movement.Systems;
using Content.Server.Body.Components;
using Content.Shared.Mind.Components;
using System.Diagnostics.CodeAnalysis;
using Robust.Shared.Timing;
using Robust.Shared.Configuration;
using Robust.Shared.Utility;
using Content.Shared.CCVar;
using Content.Shared.PowerCell.Components;
using Content.Shared.Mind;
using Content.Shared.Alert;
using Content.Server._EE.Silicon.Death;
using Content.Server._EE.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared.Atmos.Components;
using Content.Shared.Power.Components;
using Content.Shared.PowerCell;
using Content.Shared.Temperature.Components;
using NetCord;
using Robust.Shared.Prototypes;

namespace Content.Server._EE.Silicon.Charge;

public sealed partial class SiliconChargeSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private FlammableSystem _flammable = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private MovementSpeedModifierSystem _moveMod = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private PowerCellSystem _powerCell = default!;
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private BatterySystem _battery = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    private readonly ProtoId<AlertPrototype> _batteryAlert = "AiBattery";
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SiliconComponent, ComponentStartup>(OnSiliconStartup);
    }

    public bool TryGetSiliconBattery(EntityUid silicon, [NotNullWhen(true)] out Entity<BatteryComponent>? battery)
    {
        battery = null;
        if (!HasComp<SiliconComponent>(silicon))
            return false;

        if (_powerCell.TryGetBatteryFromSlot(silicon, out var batterywah))
        {
            battery = (batterywah.Value.Owner, batterywah.Value.Comp);
            return true;
        }

        //DebugTools.Assert("SiliconComponent does not contain Battery");
        return false;
    }

    private void OnSiliconStartup(EntityUid uid, SiliconComponent component, ComponentStartup args)
    {
        if (!HasComp<PowerCellSlotComponent>(uid))
            return;

        if (component.EntityType.GetType() != typeof(SiliconType))
            DebugTools.Assert("SiliconComponent.EntityType is not a SiliconType enum.");
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // For each siliconComp entity with a battery component, drain their charge.
        var query = EntityQueryEnumerator<SiliconComponent>();
        while (query.MoveNext(out var silicon, out var siliconComp))
        {
            if (_mobState.IsDead(silicon)
                || !siliconComp.BatteryPowered)
                continue;

            // Check if the Silicon is an NPC, and if so, follow the delay as specified in the CVAR.
            if (siliconComp.EntityType.Equals(SiliconType.Npc))
            {
                var updateTime = _config.GetCVar(CCVars.SiliconNpcUpdateTime);
                if (_timing.CurTime - siliconComp.LastDrainTime < TimeSpan.FromSeconds(updateTime))
                    continue;

                siliconComp.LastDrainTime = _timing.CurTime;
            }

            // If you can't find a battery, set the indicator and skip it.
            if (!TryGetSiliconBattery(silicon, out var battery))
            {
                UpdateChargeState(silicon, 0, siliconComp);
                if (_alerts.IsShowingAlert(silicon, siliconComp.BatteryAlert))
                {
                    _alerts.ClearAlert(silicon, siliconComp.BatteryAlert);
                    _alerts.ShowAlert(silicon, siliconComp.NoBatteryAlert);
                }
                continue;
            }

            // If the silicon ghosted or is SSD while still being powered, skip it.
            if (TryComp<MindContainerComponent>(silicon, out var mindContComp)
                && !mindContComp.HasMind)
                continue;

            var drainRate = siliconComp.DrainPerSecond;

            // All multipliers will be subtracted by 1, and then added together, and then multiplied by the drain rate. This is then added to the base drain rate.
            // This is to stop exponential increases, while still allowing for less-than-one multipliers.

            // Drain the battery.
            _powerCell.TryUseCharge(silicon, frameTime * drainRate);
            // Figure out the current state of the Silicon.
            var chargeLevel = (short) MathF.Round(_battery.GetChargeLevel(battery.Value.AsNullable()) * 10f);

            UpdateChargeState(silicon, chargeLevel, siliconComp);
        }
    }

    /// <summary>
    ///     Checks if anything needs to be updated, and updates it.
    /// </summary>
    public void UpdateChargeState(EntityUid uid, short chargeLevel, SiliconComponent component)
    {
        component.ChargeState = chargeLevel;
        RaiseNetworkEvent(new SiliconChargeStateUpdateEvent(chargeLevel), uid );


        _moveMod.RefreshMovementSpeedModifiers(uid);
        if (!_proto.TryIndex(_batteryAlert, out var proto))
            return;
        _alerts.ShowAlert(uid, component.BatteryAlert, (short)Math.Clamp(chargeLevel, 0f, proto.MaxSeverity));
        // If the battery was replaced and the no battery indicator is showing, replace the indicator
        if (_alerts.IsShowingAlert(uid, component.NoBatteryAlert) && chargeLevel != 0)
        {
            _alerts.ClearAlert(uid, component.NoBatteryAlert);
            _alerts.ShowAlert(uid, component.BatteryAlert, chargeLevel);
        }
    }
}
