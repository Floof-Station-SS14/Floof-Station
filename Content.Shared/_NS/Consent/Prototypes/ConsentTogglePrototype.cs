using Robust.Shared.Prototypes;

namespace Content.Shared._NS.Consent.Prototypes;

[Prototype("consentToggle")]
public sealed partial class ConsentTogglePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public LocId Name { get; private set; } = string.Empty;
}
