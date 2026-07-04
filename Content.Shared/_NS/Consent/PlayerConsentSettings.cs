using System.Linq;
using Content.Shared._NS.CCVars;
using Content.Shared._NS.Consent.Prototypes;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._NS.Consent;

/// <summary>
///     Ported from https://github.com/Fansana/floofstation1/pull/4/
/// </summary>
[Serializable] [NetSerializable]
public sealed class PlayerConsentSettings(
    string freetext)
{
    public string Freetext = freetext;

    public PlayerConsentSettings() : this(string.Empty)
    {
    }

    public void EnsureValid(IConfigurationManager configManager, IPrototypeManager prototypeManager)
    {
        var maxLength = configManager.GetCVar(NSCVars.ConsentFreetextMaxLength);
        Freetext = Freetext.Trim();
        if (Freetext.Length > maxLength)
            Freetext = Freetext.Substring(0, maxLength);
    }
}
