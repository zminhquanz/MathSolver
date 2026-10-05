namespace InteractionFeedbackWindowsTests;

public sealed partial class SmokeApplication : Microsoft.UI.Xaml.Application
{
    public SmokeApplication() => InitializeComponent();

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args) => Program.RunTests();
}
