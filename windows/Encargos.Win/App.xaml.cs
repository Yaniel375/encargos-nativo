using Microsoft.UI.Xaml;

namespace Encargos.Win;

public partial class App : Application
{
    public static MainWindow? Window { get; private set; }
    public App()
    {
        // El modo día/noche se elige antes de crear la interfaz (igual que en Android).
        try { RequestedTheme = Services.DayMode() ? ApplicationTheme.Light : ApplicationTheme.Dark; } catch { }
        InitializeComponent();
        UnhandledException += (_, e) => { e.Handled = true; Services.Report(e.Exception); };
    }
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Window = new MainWindow();
        Window.Activate();
    }
}
