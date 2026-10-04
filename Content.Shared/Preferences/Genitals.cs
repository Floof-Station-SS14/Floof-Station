using Robust.Shared.Serialization;

namespace Content.Shared.Preferences;

[Serializable, NetSerializable]
public sealed class Genitals
{
    public bool Vagina { get; set; }
    public bool Penis { get; set; }
    public bool Breasts { get; set; }
}