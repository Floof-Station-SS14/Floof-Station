using Content.Shared.Body;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Robust.Shared.Timing;

namespace Content.Shared._Floof.Body;

public sealed partial class BodyFluidProducerSystem : EntitySystem
{
    [Dependency] private HungerSystem _hunger = default!;
    [Dependency] private ThirstSystem _thirst = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedSolutionContainerSystem _solutionContainerSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BodyFluidProducerComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(EntityUid uid, BodyFluidProducerComponent component, MapInitEvent args)
    {
        component.NextGrowth = _timing.CurTime + component.GrowthDelay;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<BodyFluidProducerComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (_timing.CurTime < comp.NextGrowth)
                continue;

            comp.NextGrowth += comp.GrowthDelay;

            if (!TryComp(uid, out OrganComponent? organ))
                continue;

            if (organ.Body is not EntityUid body) //I haven't tested this, probably should but surgery isn't a thing yet
                continue;

            if (_mobState.IsDead(body))
                continue;

            if (!_solutionContainerSystem.ResolveSolution(uid, comp.SolutionName, ref comp.Solution, out var solution))
                continue;

            if (solution.AvailableVolume == 0)
                continue;
            
            if (!TryComp(body, out HungerComponent? hunger))
                continue;

            if (_hunger.GetHungerThreshold(hunger) < HungerThreshold.Okay)
                continue;

            if (!TryComp(body, out ThirstComponent? thirst))
                continue;

            //You gotta wonder why the hunger system had the cool update but the thirst system was neglected.
            if (thirst.CurrentThirstThreshold < ThirstThreshold.Okay)
                continue;

            _solutionContainerSystem.TryAddReagent(comp.Solution.Value, comp.ReagentId, comp.QuantityPerUpdate,
                out var quantity);
            
            if (quantity == 0)
                continue;
            
            _hunger.ModifyHunger(body, -comp.HungerUsage * quantity.Float(), hunger);
            _thirst.ModifyThirst(body, thirst, -comp.ThirstUsage * quantity.Float());
            
        }
    }
}