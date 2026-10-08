using Content.Server._Floof.Consent.Managers;
using Content.Server.Mind;
using Content.Shared._Floof.Consent.Prototypes;
using Content.Shared._Floof.Consent.Systems;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server._Floof.Consent.Systems;

public sealed partial class ConsentSystem : SharedConsentSystem
{
    [Dependency] private IServerConsentManager _consent = default!;

    protected override FormattedMessage GetConsentText(NetUserId userId)
    {
        var text = Loc.GetString("consent-examine-not-set");

        if (_consent.TryGetPlayerConsentSettings(userId, out var consent))
        {
            text = consent.Freetext;
        }

        var message = new FormattedMessage();
        message.AddText(text);
        return message;
    }
}
