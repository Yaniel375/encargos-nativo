using Microsoft.UI.Xaml;

namespace Encargos.Win;

public partial class App : Application
{
    public static MainWindow? Window { get; private set; }
    public App()
    {
        // Base oscura; el modo día/noche lo aplica MainWindow.ApplyTheme al instante (sin reiniciar).
        try { RequestedTheme = ApplicationTheme.Dark; } catch { }
        InitializeComponent();
        UnhandledException += (_, e) => { e.Handled = true; Services.Report(e.Exception); };
    }
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Window = new MainWindow();
        Window.Activate();
    }
}
