using Hiker.Services;

namespace Hiker.Pages;

public partial class AboutPage : ContentPage
{
    private const string ContactEmail = "jsoladelarosa@gmail.com";
    private const string KofiUrl = "https://ko-fi.com/josepsola";
    private const string AppVersion = "2026.07.17.0";

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
    }

    /// <summary>
    /// Resuelve el texto con el servicio de traducción. Si falta la traducción, el servicio
    /// devuelve la frase nativa (español), sin marcadores técnicos.
    /// </summary>
    private string T(string nativePhrase) => _translation?.Translate(nativePhrase) ?? nativePhrase;

    private void UpdateTexts()
    {
        Title = T("Acerca de");
        VersionLabel.Text = $"{T("Versión")} {AppVersion}";
        DescriptionLabel.Text = T("Tu compañero de aventuras al aire libre");

        ContactTitleLabel.Text = T("Contacto");
        ContactInstructionLabel.Text = T("Toca para enviar un correo electrónico");

        SupportTitleLabel.Text = T("Apoya el Desarrollo");
        DonationButton.Text = T("Ko-fi.com - Invítame un café");
        SupportDescLabel.Text = T("Tu apoyo ayuda a mantener y mejorar la aplicación");

        LegalTitleLabel.Text = T("Aviso Legal");
        LegalTextLabel.Text = T("Hiker se proporciona «tal cual», sin garantías de ningún tipo. El usuario es responsable del uso adecuado de la aplicación y del cumplimiento de las leyes locales.");
        LicenseLabel.Text = T("Licencia MIT · © 2026 Socratic");
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

    private async void OnDonationClicked(object? sender, EventArgs e)
    {
        try
        {
            await Browser.Default.OpenAsync(new Uri(KofiUrl), BrowserLaunchMode.SystemPreferred);
        }
        catch
        {
            try
            {
                await Clipboard.SetTextAsync(KofiUrl);
                await DisplayAlert(T("Apoya el Desarrollo"), $"{KofiUrl}", "OK");
            }
            catch { /* nada mas que hacer */ }
        }
    }
}
