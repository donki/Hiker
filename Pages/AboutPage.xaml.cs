using Hiker.Services;

namespace Hiker.Pages;

/// <summary>
/// Pantalla «Acerca de»: informacion de la app, contacto, apoyo (Ko-fi), seleccion de idioma
/// y las declaraciones de privacidad, licencia y aviso legal. Estructura comun a todo el
/// repositorio de aplicaciones (ver constitucion, anexo A.9).
/// </summary>
public partial class AboutPage : ContentPage
{
    private const string ContactEmail = "jsoladelarosa@gmail.com";

    private TranslationService? _translation;

    public AboutPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _translation ??= Handler?.MauiContext?.Services.GetService<TranslationService>();
        UpdateTexts();
        UpdateLanguageButtons();
    }

    /// <summary>
    /// Resuelve el texto con el servicio de traduccion. Si falta la traduccion, el servicio
    /// devuelve la frase nativa (espanol), sin marcadores tecnicos.
    /// </summary>
    private string T(string nativePhrase) => _translation?.Translate(nativePhrase) ?? nativePhrase;

    private void UpdateTexts()
    {
        Title = T("Acerca de");
        VersionLabel.Text = $"{T("Versión")} {AppInfo.Current.VersionString}";
        DescriptionLabel.IsVisible = false;

        ContactTitleLabel.Text = T("Contacto");
        ContactHintLabel.IsVisible = false;


        LanguageTitleLabel.Text = T("Idioma");
        LanguageHintLabel.Text = T("Selecciona tu idioma preferido");

        PrivacyTitleLabel.Text = T("Privacidad");
        PrivacyTextLabel.Text = T("Esta aplicación no recopila tus datos personales. La información se procesa en tu dispositivo para la función propia de la app.");

        LicenseTitleLabel.Text = T("Licencia");
        LicenseTextLabel.Text = T("Esta aplicación es software libre distribuido bajo licencia MIT.");

        LegalTitleLabel.Text = T("Aviso Legal");
        LegalText1Label.Text = T("Este software se proporciona «tal cual», sin garantías de ningún tipo. El usuario es responsable del uso adecuado de la aplicación y del cumplimiento de las leyes locales.");
        LegalText2Label.Text = T("En ningún caso los autores serán responsables de daños directos, indirectos, incidentales o consecuentes que resulten del uso de este software.");
        LegalWarningLabel.Text = T("⚠️ Uso bajo su propio riesgo");
    }

    private void UpdateLanguageButtons()
    {
        var spanishActive = (_translation?.GetCurrentLanguage() ?? "es") == "es";
        StyleLanguageButton(SpanishButton, spanishActive);
        StyleLanguageButton(EnglishButton, !spanishActive);
    }

    // El idioma activo se resalta por estilo (relleno de marca) y el inactivo con contorno.
    private void StyleLanguageButton(Button button, bool active)
    {
        var key = active ? "PrimaryButton" : "OutlineButton";
        if (Resources.TryGetValue(key, out var style) ||
            Application.Current!.Resources.TryGetValue(key, out style))
        {
            button.Style = (Style)style;
        }
    }

    private void OnSpanishClicked(object? sender, EventArgs e) => SetLanguage("es");

    private void OnEnglishClicked(object? sender, EventArgs e) => SetLanguage("en");

    private void SetLanguage(string languageCode)
    {
        _translation?.SetLanguage(languageCode);
        UpdateTexts();
        UpdateLanguageButtons();
    }

    private async void OnContactClicked(object? sender, EventArgs e)
    {
        try
        {
            await Launcher.OpenAsync($"mailto:{ContactEmail}?subject=Hiker");
        }
        catch (Exception ex)
        {
            await SocShared.ModernDialog.AlertAsync(this, T("Error"), $"{T("No se pudo abrir el cliente de correo")}: {ex.Message}", "OK");
        }
    }

}
