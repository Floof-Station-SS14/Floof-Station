using Content.Shared._NS.Consent;

namespace Content.Client._NS.Consent.Managers;

/// <summary>
/// Ported from https://github.com/Fansana/floofstation1/pull/4/
/// </summary>
public interface IClientConsentManager
{
    event Action OnServerDataLoaded;
    bool HasLoaded { get; }

    void Initialize();
    void UpdateConsent(PlayerConsentSettings consentSettings);
    PlayerConsentSettings GetConsent();
}
