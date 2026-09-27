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
        
        // La primera vez que aparece la pagina el Handler aun no tiene MauiContext: sin la caida al
        // proveedor global, el servicio se quedaba a null, la pantalla salia siempre en castellano
        // con «Español» marcado y los botones de idioma no hacian nada.
        _translationService ??= (Handler?.MauiContext?.Services ?? IPlatformApplication.Current?.Services)
            ?.GetService<TranslationService>();

        LoadSettings();
    }

    // Solo queda el idioma: se guarda solo al pulsar (TranslationService.SetLanguage). La
    // configuracion del GPS y los botones Guardar/Restablecer se quitaron a peticion.
    private void LoadSettings()
    {
        TranslateUi();
        UpdateLanguageButtons();
    }

    /// <summary>Textos de la pantalla en el idioma activo (constitucion seccion 8).</summary>
    private void TranslateUi()
    {
        string L(string phrase) => _translationService?.Translate(phrase) ?? phrase;

        Title = L("Configuración");
        languageTitleLabel.Text = L("Idioma");
        languageHintLabel.Text = L("Selecciona tu idioma preferido");
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
        TranslateUi();
        UpdateLanguageButtons();

        // El menu lateral ya esta pintado: se le pide que se retraduzca.
        (Shell.Current as AppShell)?.ApplyTranslations();
    }
}
