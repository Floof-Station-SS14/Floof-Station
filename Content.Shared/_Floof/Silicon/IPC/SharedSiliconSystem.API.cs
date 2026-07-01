using System.Diagnostics.CodeAnalysis;
using Content.Shared._EE.Silicon.Components;
using Content.Shared.Power.Components;

namespace Content.Shared._Floof.Silicon.IPC;

public partial class SharedSiliconSystem
{
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

}
