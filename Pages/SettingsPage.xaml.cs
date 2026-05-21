using Hiker.Services;

namespace Hiker.Pages;

public partial class SettingsPage : ContentPage
{
    private SettingsService? _settingsService;
    private TranslationService? _translationService;

    public SettingsPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        
        // Obtener servicios cuando el Handler esté disponible
        if (Handler?.MauiContext?.Services != null)
        {
            _settingsService = Handler.MauiContext.Services.GetService<SettingsService>();
            _translationService = Handler.MauiContext.Services.GetService<TranslationService>();
        }
        
        LoadSettings();
    }

    private void LoadSettings()
    {
        if (_settingsService == null) return;
        
        var settings = _settingsService.AppSettings;
        
        nativeGpsSwitch.IsToggled = settings.UseNativeGeolocation;
        intervalEntry.Text = settings.TimerInterval.ToString();
        accuracyEntry.Text = settings.Accuracy.ToString();
        kalmanSwitch.IsToggled = settings.KalmanFilterEnabled;
        speedFilterSwitch.IsToggled = settings.SpeedFilterEnabled;
        maxSpeedEntry.Text = settings.MaxSpeed.ToString();
        
        // Configurar idioma
        if (_translationService != null)
        {
            var currentLanguage = _translationService.GetCurrentLanguage();
            languagePicker.SelectedIndex = currentLanguage == "es" ? 0 : 1;
        }
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        try
        {
            if (_settingsService == null) return;
            
            var settings = _settingsService.AppSettings;
            
            settings.UseNativeGeolocation = nativeGpsSwitch.IsToggled;
            
            if (int.TryParse(intervalEntry.Text, out int interval))
                settings.TimerInterval = Math.Max(1, interval);
            
            if (int.TryParse(accuracyEntry.Text, out int accuracy))
                settings.Accuracy = Math.Max(1, accuracy);
            
            settings.KalmanFilterEnabled = kalmanSwitch.IsToggled;
            settings.SpeedFilterEnabled = speedFilterSwitch.IsToggled;
            
            if (double.TryParse(maxSpeedEntry.Text, out double maxSpeed))
                settings.MaxSpeed = Math.Max(1, maxSpeed);

            await _settingsService.SaveSettingsAsync();
            
            // Configurar idioma
            if (_translationService != null)
            {
                var selectedLanguage = languagePicker.SelectedIndex == 0 ? "es" : "en";
                _translationService.SetLanguage(selectedLanguage);
            }
            
            await DisplayAlert("Éxito", "Configuración guardada correctamente", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Error guardando configuración: {ex.Message}", "OK");
        }
    }

    private async void OnResetClicked(object sender, EventArgs e)
    {
        try
        {
            var confirm = await DisplayAlert("Confirmar", 
                "¿Restablecer configuración a valores por defecto?", "Sí", "No");
            
            if (confirm && _settingsService != null)
            {
                await _settingsService.ResetSettingsAsync();
                LoadSettings();
                await DisplayAlert("Éxito", "Configuración restablecida", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Error restableciendo configuración: {ex.Message}", "OK");
        }
    }
}