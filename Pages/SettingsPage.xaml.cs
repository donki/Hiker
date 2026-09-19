using Hiker.Services;

namespace Hiker.Pages;

public partial class SettingsPage : ContentPage
{
    // Verde de marca de Hiker para resaltar el idioma activo.
    private static readonly Color ActiveLanguage = Color.FromArgb("#2E7D32");

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
            _translationService = Handler.MauiContext.Services.GetService<TranslationService>();
        }
        
        LoadSettings();
    }

    // Solo queda el idioma: se guarda solo al pulsar (TranslationService.SetLanguage). La
    // configuracion del GPS y los botones Guardar/Restablecer se quitaron a peticion.
    private void LoadSettings() => UpdateLanguageButtons();

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
}
