using Content.Server._EE.Silicon.WeldingHealing;
using Content.Shared.Tools.Components;
using Content.Shared._EE.Silicon.WeldingHealing;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Tools;
using SharedToolSystem = Content.Shared.Tools.Systems.SharedToolSystem;

namespace Content.Server._EE.Silicon.WeldingHealable;

public sealed partial class WeldingHealableSystem : SharedWeldingHealableSystem
{
    [Dependency] private SharedToolSystem _toolSystem = default!;
    [Dependency] private DamageableSystem _damageableSystem = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedSolutionContainerSystem _solutionContainer = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<WeldingHealableComponent, InteractUsingEvent>(Repair);
        SubscribeLocalEvent<WeldingHealableComponent, SiliconRepairFinishedEvent>(OnRepairFinished);
    }

    private void OnRepairFinished(EntityUid uid, WeldingHealableComponent healableComponent, SiliconRepairFinishedEvent args)
    {
        if (args.Cancelled || args.Used == null
            || !TryComp<DamageableComponent>(args.Target, out var damageable)
            || !TryComp<WeldingHealingComponent>(args.Used, out var component)
         //   || damageable.DamageContainerID is null
        //    || !component.DamageContainers.Contains(damageable.DamageContainerID)
            || !HasDamage(damageable, component)
            || !TryComp<WelderComponent>(args.Used, out var welder)
            || !TryComp<SolutionManagerComponent>(args.Used, out var solutionContainer)
            || !_solutionContainer.TryGetSolution(((EntityUid) args.Used, solutionContainer), welder.FuelSolutionName, out var solution))
            return;

        _damageableSystem.TryChangeDamage(uid, component.Damage, true, false, origin: args.User);

        _solutionContainer.RemoveReagent(solution.Value, welder.FuelReagent, component.FuelCost);

        var str = Loc.GetString("comp-repairable-repair",
            ("target", uid),
            ("tool", args.Used!));
        _popup.PopupEntity(str, uid, args.User);

        if (!args.Used.HasValue)
            return;

        args.Handled = _toolSystem.UseTool
            (args.Used.Value,
            args.User,
            uid,
            args.Delay,
            component.QualityNeeded,
            new SiliconRepairFinishedEvent
            {
                Delay = args.Delay
            });
    }
   private async void Repair(EntityUid uid, WeldingHealableComponent healableComponent, InteractUsingEvent args)
   {
        if (args.Handled
           || !TryComp<WeldingHealingComponent>(args.Used, out WeldingHealingComponent? component)
           || !TryComp<DamageableComponent>(args.Target, out DamageableComponent? damageable)
           //  || damageable.DamageContainerID is null
           //  || !component.DamageContainers.Contains(damageable.DamageContainerID)
           || !HasDamage(damageable, component)
           || !_toolSystem.HasQuality(args.Used, component.QualityNeeded)
           || args.User == args.Target && !component.AllowSelfHeal)
            return;

        float delay = args.User == args.Target
            ? component.DoAfterDelay * component.SelfHealPenalty
            : component.DoAfterDelay;

        args.Handled = _toolSystem.UseTool
            (args.Used,
            args.User,
            args.Target,
            delay,
            component.QualityNeeded,
            new SiliconRepairFinishedEvent
            {
                Delay = delay,
            });
    }

 private bool HasDamage(DamageableComponent component, WeldingHealingComponent healable)
    {
        if (healable.Damage.DamageDict is null)
            return false;

        foreach (var type in healable.Damage.DamageDict)
            if (_damageableSystem.GetPositiveDamage((component.Owner, component)).DamageDict[type.Key] > 0)
                return true;

        return false;
    }
}
