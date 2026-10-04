namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private void GenitalsInit()
    {
        Penis.OnToggled += args =>
        {
            ReloadPreview();
        };
        Vagina.OnToggled += args =>
        {
            ReloadPreview();
        };
        Breasts.OnToggled += args =>
        {
            ReloadPreview();
        };
    }
    private void SetGenitals(bool penis, bool vagina, bool breasts)
    {
        Penis.Pressed = penis;
        Vagina.Pressed = vagina;
        Breasts.Pressed = breasts;
    }

    private void UpdateGenitals()
    {
        
    }
}