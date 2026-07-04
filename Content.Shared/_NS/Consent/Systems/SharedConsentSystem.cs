using Content.Shared.Administration.Managers;
using Content.Shared.Examine;
using Content.Shared.Ghost;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Verbs;
using Robust.Shared.Network;
using Robust.Shared.Utility;

namespace Content.Shared._NS.Consent.Systems;

public abstract partial class SharedConsentSystem : EntitySystem
{
    [Dependency] private readonly SharedMindSystem _mindSystem = default!;
    [Dependency] private readonly ExamineSystemShared _examineSystem = default!;
    [Dependency] private readonly ISharedAdminManager _adminManager = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<MindContainerComponent, GetVerbsEvent<ExamineVerb>>(OnGetExamineVerbs);
    }

    private void OnGetExamineVerbs(EntityUid uid, MindContainerComponent component, GetVerbsEvent<ExamineVerb> args)
    {
        if (_mindSystem.GetMind(uid) is not { } mind
            || !TryComp<MindComponent>(mind, out var mindComponent)
            || mindComponent.UserId == null)
        {
            return;
        }

        // Ghost check, get fucked lol. You know who you are
        if (HasComp<GhostComponent>(args.User) && !_adminManager.IsAdmin(args.User, true))
            return;

        args.Verbs.Add(new()
        {
            Text = Loc.GetString("consent-examine-verb"),
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/settings.svg.192dpi.png")),
            Act = () =>
            {
                var message = GetConsentText(mindComponent.UserId.Value);
                _examineSystem.SendExamineTooltip(args.User, uid, message, false, false);
            },
            Category = VerbCategory.Examine,
            CloseMenu = true,
        });
    }

    protected virtual FormattedMessage GetConsentText(NetUserId userId)
    {
        return new FormattedMessage();
    }
}
