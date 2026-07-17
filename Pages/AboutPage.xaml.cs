using System.Reflection;
using Hiker.Services;

namespace Hiker.Pages;

public partial class AboutPage : ContentPage
{
    private const string ContactEmail = "support@hiker.app";

    private TranslationService? _translation;
    private bool _updatingPicker;

    public AboutPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _translation ??= Handler?.MauiContext?.Services.GetService<TranslationService>();
        SetupLanguagePicker();
        UpdateTexts();
        LoadSystemInfo();
    }

    /// <summary>
    /// Resuelve el texto con el servicio de traducción de la app. Si no hay servicio o falta la
    /// traducción, el propio servicio devuelve la frase nativa (español), sin marcadores técnicos.
    /// </summary>
    private string T(string nativePhrase) => _translation?.Translate(nativePhrase) ?? nativePhrase;

    private void UpdateTexts()
    {
        Title = T("Acerca de");
        DescriptionLabel.Text = T("Tu compañero de aventuras al aire libre");
        ContactTitleLabel.Text = T("Contacto");
        ContactInstructionLabel.Text = T("Toca para enviar un correo electrónico");
        LegalTitleLabel.Text = T("Aviso Legal");
        LegalTextLabel.Text = T("Hiker se proporciona «tal cual», sin garantías de ningún tipo. El usuario es responsable del uso adecuado de la aplicación y del cumplimiento de las leyes locales.");
        LicenseLabel.Text = T("Licencia MIT · © 2026 Socratic");
        LanguageTitleLabel.Text = T("Idioma");
        LanguageInstructionLabel.Text = T("Selecciona tu idioma preferido");
        SystemTitleLabel.Text = T("Información del Sistema");
    }

    private void SetupLanguagePicker()
    {
        if (_translation is null)
            return;

        _updatingPicker = true;
        try
        {
            var codes = _translation.GetSupportedLanguages();
            LanguagePicker.ItemsSource = codes.Select(_translation.GetLanguageName).ToList();

            var current = _translation.GetCurrentLanguage();
            var index = Array.IndexOf(codes, current);
            if (index >= 0)
                LanguagePicker.SelectedIndex = index;
        }
        finally
        {
            _updatingPicker = false;
        }
    }

    private void OnLanguagePickerChanged(object? sender, EventArgs e)
    {
        if (_updatingPicker || _translation is null || LanguagePicker.SelectedIndex < 0)
            return;

        var codes = _translation.GetSupportedLanguages();
        var selected = codes[LanguagePicker.SelectedIndex];
        if (selected == _translation.GetCurrentLanguage())
            return;

        _translation.SetLanguage(selected);
        UpdateTexts();
    }

    private void LoadSystemInfo()
    {
        try
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            VersionLabel.Text = $"{T("Versión")} {version?.ToString(3) ?? "1.9.326"}";

            platformLabel.Text = $"{T("Plataforma")}: {DeviceInfo.Platform}";
            deviceLabel.Text = $"{T("Dispositivo")}: {DeviceInfo.Model}";
            osVersionLabel.Text = $"{T("Versión del sistema")}: {DeviceInfo.VersionString}";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading system info: {ex.Message}");
        }
    }

    private async void OnContactClicked(object? sender, EventArgs e)
    {
        try
        {
            await Launcher.OpenAsync($"mailto:{ContactEmail}?subject=Hiker");
        }
        catch (Exception ex)
        {
            await DisplayAlert(T("Error"), $"{T("No se pudo abrir el cliente de correo")}: {ex.Message}", "OK");
        }
    }
}
