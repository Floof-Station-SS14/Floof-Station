using Robust.Shared.Prototypes;
using Content.Shared._Starlight.Language;
using Content.Shared._Starlight.Language.Systems;

namespace Content.Shared._DV.Traits.Effects;

public sealed partial class AddLangEffect : BaseTraitEffect
{
    // Starlight - Start
    /// <summary>
    ///     The list of all Spoken Languages that this trait adds.
    /// </summary>
    [DataField]
    public List<ProtoId<LanguagePrototype>>? LanguagesSpoken { get; private set; } = default!;

    /// <summary>
    ///     The list of all Understood Languages that this trait adds.
    /// </summary>
    [DataField]
    public List<ProtoId<LanguagePrototype>>? LanguagesUnderstood { get; private set; } = default!;

    public override void Apply(TraitEffectContext ctx)
    {
        var language = ctx.EntMan.System<SharedLanguageSystem>();

        if (LanguagesSpoken is not null)
            foreach (var lang in LanguagesSpoken)
                language.AddLanguage(ctx.Player, lang, true, false);

        if (LanguagesUnderstood is not null)
            foreach (var lang in LanguagesUnderstood)
                language.AddLanguage(ctx.Player, lang, false, true);
    }
}
