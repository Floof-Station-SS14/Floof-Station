using Content.Server.Power.EntitySystems;
using Content.Shared._EE.Silicon.Components;
using Content.Shared._Floof.Silicon.IPC;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Movement.Systems;
using Content.Shared.PowerCell.Components;

namespace Content.Server._Floof.Silicon.IPC;

public sealed partial class SiliconChargeSystem : SharedSiliconSystem
{

    [Dependency] private MovementSpeedModifierSystem _moveMod = default!;
    [Dependency] private BatterySystem _battery = default!;
    protected override void OnUpdate(EntityUid uid, SiliconComponent siliconBody)
    {
        base.OnUpdate(uid, siliconBody);


    }

}
