using System.Numerics;
using Content.Shared._DV.Traits.Effects;
using Content.Shared.Body;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Shared._Floof.Traits.Effects;

/// <summary>
/// Effect that adds components to the player entity.
/// Components are added without overwriting existing ones.
/// </summary>
public sealed partial class AddOrganEffect : BaseTraitEffect
{
    
    [DataField(required: true)]
    public required Dictionary<ProtoId<OrganCategoryPrototype>, EntProtoId<OrganComponent>> Organs;
    
    [DataField]
    public Dictionary<ProtoId<OrganCategoryPrototype>, HashSet<ProtoId<OrganCategoryPrototype>>>? Relationships;
    
    public override void Apply(TraitEffectContext ctx)
    {
        var transform = ctx.EntMan.System<SharedTransformSystem>();
        var containerSystem = ctx.EntMan.System<SharedContainerSystem>();
        var organRelation = ctx.EntMan.System<OrganRelationSystem>();
        
        if (!ctx.EntMan.TryGetComponent<ContainerManagerComponent>(ctx.Player, out var containerComp))
            return;
        
        if (!containerSystem.TryGetContainer(ctx.Player, BodyComponent.ContainerID, out var container, containerComp))
            return;
        
        
        var xform = ctx.EntMan.GetComponent<TransformComponent>(ctx.Player);
        var coords = transform.ToMapCoordinates(new EntityCoordinates(ctx.Player, Vector2.Zero));
        var spawned = new Dictionary<ProtoId<OrganCategoryPrototype>, EntityUid>();
        
        foreach (var (organCategory, proto) in Organs)
        {
            var spawn = ctx.EntMan.Spawn(proto.Id, coords);

            if (!containerSystem.Insert(spawn, container, containerXform: xform))
            {
                ctx.EntMan.DeleteEntity(spawn);
                continue;
            }
            spawned[organCategory] = spawn;
        }
        
        if (Relationships is null)
            return;

        foreach (var (parentCategory, childCategorySet) in Relationships)
        {
            Entity<ParentOrganComponent?>? parentOrgan = null;
            foreach (var organ in container.ContainedEntities)
            {
                var organComp = ctx.EntMan.GetComponentOrNull<OrganComponent>(organ);
                var parentOrganComp = ctx.EntMan.GetComponentOrNull<ParentOrganComponent>(organ);
                if (organComp != null && organComp.Category == parentCategory)
                {
                    parentOrgan = (organ, parentOrganComp);
                }
            }

            if (parentOrgan is not { } parent)
                continue;
            
            foreach (var childOrganCategory in childCategorySet)
            {
                if (!spawned.TryGetValue(childOrganCategory, out var childUid))
                    continue;
                
                organRelation.Relate(parent, childUid);
            }
        }
    }
}