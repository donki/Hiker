using Hiker.Services;

namespace Hiker.Pages;

public partial class SettingsPage : ContentPage
{
    // Verde de marca de Hiker para resaltar el idioma activo.
    private static readonly Color ActiveLanguage = Color.FromArgb("#2E7D32");

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
        
        UpdateLanguageButtons();
    }

    private void UpdateLanguageButtons()
    {
        var spanishActive = (_translationService?.GetCurrentLanguage() ?? "es") == "es";
        StyleLanguageButton(spanishButton, spanishActive);
        StyleLanguageButton(englishButton, !spanishActive);
    }

    private static void StyleLanguageButton(Button button, bool active)
    {
        button.BackgroundColor = active ? ActiveLanguage : Colors.Transparent;
        button.TextColor = active ? Colors.White : ActiveLanguage;
        button.BorderColor = ActiveLanguage;
        button.BorderWidth = active ? 0 : 1;
    }

    private void OnSpanishClicked(object? sender, EventArgs e) => ApplyLanguage("es");

    private void OnEnglishClicked(object? sender, EventArgs e) => ApplyLanguage("en");

    private void ApplyLanguage(string languageCode)
    {
        _translationService?.SetLanguage(languageCode);
        UpdateLanguageButtons();
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

            // El idioma se aplica de inmediato al pulsar los botones Español/English.

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