using Content.Client._Floof.Consent;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private NewConsentText? _consentText;
    private TextEdit? _consentTextEdit;

    /// <summary>
    /// Refreshes the Consent text editor status.
    /// </summary>
    public void RefreshConsentText()
    {
        if (_consentText != null)
            return;

        _consentText = new NewConsentText();
        TabContainer.AddChild(_consentText);
        TabContainer.SetTabTitle(TabContainer.ChildCount - 1, Loc.GetString("humanoid-profile-editor-consent-tab"));
        _consentTextEdit = _consentText.CConsentTextInput;

        _consentText.OnConsentTextChanged += OnConsentTextChange;
    }

    private void OnConsentTextChange(string content)
    {
        if (Profile is null)
            return;

        Profile = Profile.WithConsentText(content);
        SetDirty();
    }

    private void UpdateConsentTextEdit()
    {
        _consentTextEdit?.TextRope = new Rope.Leaf(Profile?.ConsentText ?? "");
    }
}
