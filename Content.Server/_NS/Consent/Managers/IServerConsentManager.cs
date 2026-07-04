using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Content.Shared._NS.Consent;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server._NS.Consent.Managers;

public interface IServerConsentManager
{
    void Initialize();

    Task LoadData(ICommonSession session, CancellationToken cancel);
    void OnClientDisconnected(ICommonSession session);

    /// <summary>
    /// Get player consent settings
    /// </summary>
    bool TryGetPlayerConsentSettings(NetUserId userId, [NotNullWhen(true)] out PlayerConsentSettings? consentSettings);
}
