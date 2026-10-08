namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private void GenitalsInit()
    {
        Penis.OnToggled += args =>
        {
            if (Profile is null)
                return;
            Profile = Profile.WithGenitals(Profile.Genitals with { Penis = args.Pressed });
            ReloadPreview();
            UpdateMarkings();
        };
        Vagina.OnToggled += args =>
        {
            if (Profile is null)
                return;
            Profile = Profile.WithGenitals(Profile.Genitals with { Vagina = args.Pressed });
            ReloadPreview();
            UpdateMarkings();
        };
        Breasts.OnToggled += args =>
        {
            if (Profile is null)
                return;
            Profile = Profile.WithGenitals(Profile.Genitals with { Breasts = args.Pressed });
            ReloadPreview();
            UpdateMarkings();
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
        if(Profile is null)
            return;
        SetGenitals(Profile.Genitals.Penis, Profile.Genitals.Vagina, Profile.Genitals.Breasts);
    }
}