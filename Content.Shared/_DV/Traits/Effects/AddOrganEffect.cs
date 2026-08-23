using Robust.Shared.Prototypes;
using Content.Shared.Body;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using System.Numerics;

namespace Content.Shared._DV.Traits.Effects;

public sealed partial class AddOrganEffect : BaseTraitEffect
{
    [DataField(required: true)]
    public EntProtoId<OrganComponent> Organ;

    public override void Apply(TraitEffectContext ctx)
    {
        var _container = ctx.EntMan.System<SharedContainerSystem>();

        if (!ctx.EntMan.TryGetComponent<ContainerManagerComponent>(ctx.Player, out var containerComp))
            return;

        //if (TerminatingOrDeleted(ctx.Player) || !Exists(ctx.Player))
            //return;

        if (!_container.TryGetContainer(ctx.Player, BodyComponent.ContainerID, out var container, containerComp))
        {
            return;
        }

        var xform = ctx.EntMan.GetComponent<TransformComponent>(ctx.Player);
        var transform = ctx.EntMan.System<SharedTransformSystem>();
        var coords = transform.ToMapCoordinates(new EntityCoordinates(ctx.Player, Vector2.Zero));

        {
            var spawn = ctx.EntMan.Spawn(Organ.Id, coords);

            if (!_container.Insert(spawn, container, containerXform: xform))
            {
                ctx.EntMan.DeleteEntity(spawn);
            }
        }
    }
}
