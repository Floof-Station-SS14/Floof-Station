using Content.Shared.Body;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.InteractionVerbs.Requirements;

/// <summary>
///     Requires the target to have a organ.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class BodyOrganRequirement : InteractionRequirement
{
    [DataField] public required ProtoId<OrganCategoryPrototype> OrganCategory;
    public override bool IsMet(InteractionArgs args, InteractionVerbPrototype proto, InteractionAction.VerbDependencies deps)
    {
        var containerSystem = deps.EntMan.System<SharedContainerSystem>();
        if (!deps.EntMan.TryGetComponent<ContainerManagerComponent>(args.Target, out var containerComp))
            return false;
        
        if (!containerSystem.TryGetContainer(args.Target, BodyComponent.ContainerID, out var container, containerComp))
            return false;

        foreach (var organ in container.ContainedEntities)
        {
            if (!deps.EntMan.TryGetComponent<OrganComponent>(organ, out var organComp))
                return false;
            
            if (organComp.Category == OrganCategory)
                return true;
        }
        
        return false;
    }
}