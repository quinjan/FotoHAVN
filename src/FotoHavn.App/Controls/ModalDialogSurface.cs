using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;

namespace FotoHavn.App.Controls;

public sealed class ModalDialogSurface : ContentControl
{
    protected override AutomationPeer OnCreateAutomationPeer() => new ModalDialogSurfaceAutomationPeer(this);

    private sealed class ModalDialogSurfaceAutomationPeer(ModalDialogSurface owner)
        : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Window;

        protected override string GetClassNameCore() => "Dialog";

    }
}
