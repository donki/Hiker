using System.Reflection;

namespace Hiker.Pages;

public partial class AboutPage : ContentPage
{
    public AboutPage()
    {
        InitializeComponent();
        LoadSystemInfo();
    }

    private void LoadSystemInfo()
    {
        try
        {
            // Información de la aplicación
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            versionLabel.Text = $"Versión {version?.ToString(3) ?? "2.0.0"} - MAUI Nativo";
            
            // Información del sistema
            platformLabel.Text = $"Plataforma: {DeviceInfo.Platform}";
            deviceLabel.Text = $"Dispositivo: {DeviceInfo.Model}";
            osVersionLabel.Text = $"Versión OS: {DeviceInfo.VersionString}";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading system info: {ex.Message}");
        }
    }

    private async void OnContactClicked(object sender, EventArgs e)
    {
        try
        {
            await Launcher.OpenAsync("mailto:support@hiker.app?subject=Hiker App Feedback");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"No se pudo abrir el cliente de correo: {ex.Message}", "OK");
        }
    }

    private async void OnRateClicked(object sender, EventArgs e)
    {
        try
        {
            await DisplayAlert("Valorar App", "¡Gracias por usar Hiker! Tu valoración nos ayuda a mejorar.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Error: {ex.Message}", "OK");
        }
    }
}