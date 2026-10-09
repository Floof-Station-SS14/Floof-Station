using Robust.Shared.Prototypes;

namespace Content.Shared._Floof.Consent.Prototypes;

[Prototype]
public sealed partial class ConsentTogglePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public LocId Name { get; private set; } = string.Empty;
}
