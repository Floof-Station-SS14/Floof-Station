using System.Numerics;
using Content.Shared.Body;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Shared._Floof.Body;

public sealed partial class OrganSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private OrganRelationSystem _organRelation = default!;

    /// <summary>
    /// Spawns an organ into the body and relates it to the first organ of the given parent category.
    /// </summary>
    public void AddOrgan(EntityUid body, EntProtoId<OrganComponent> organ, ProtoId<OrganCategoryPrototype> parentCategory)
    {
        if (!_container.TryGetContainer(body, BodyComponent.ContainerID, out var container))
            return;

        var spawned = Spawn(organ, new EntityCoordinates(body, Vector2.Zero));
        if (!_container.Insert(spawned, container))
        {
            Del(spawned);
            return;
        }

        foreach (var other in container.ContainedEntities)
        {
            if (!TryComp<OrganComponent>(other, out var organComp) || organComp.Category != parentCategory)
                continue;

            _organRelation.Relate(other, spawned);
            break;
        }
    }
}
