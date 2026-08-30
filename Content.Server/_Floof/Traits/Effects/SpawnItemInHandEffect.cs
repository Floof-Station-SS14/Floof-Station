using Content.Shared._Floof.Traits.Effects;
using Serilog;

namespace Content.Server._Floof.Traits.Effects;

public sealed partial class SpawnItemInHandEffect : SharedSpawnItemInHandEffect
{
    public override void Apply(TraitEffectContext ctx)
    {
        Log.Information("I am the server trying to apply the trait and it works.");
    }
}