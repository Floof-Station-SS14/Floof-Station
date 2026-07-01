using Content.Client.Power.EntitySystems;
using Content.Shared._EE.Silicon.Components;
using Content.Shared.Alert;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.PowerCell.Components;
using Content.Shared.Silicons.Borgs.Components;
using Robust.Client.Player;


namespace Content.Shared._Floof.Silicon.IPC;

public sealed partial class SiliconSystem : SharedSiliconSystem
{
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private BatterySystem _battery = default!;
    [Dependency] private  MobStateSystem _mobState = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private EntityQuery<SiliconComponent> _chassisQuery = default!;
    [Dependency] private EntityQuery<PowerCellSlotComponent> _slotQuery = default!;
    protected override void OnUpdate(EntityUid uid, SiliconComponent siliconBody)
    {
        base.OnUpdate(uid, siliconBody);
        if (_mobState.IsDead(uid)
           || !siliconBody.BatteryPowered)
           return;

        UpdateBattery();
    }

}
