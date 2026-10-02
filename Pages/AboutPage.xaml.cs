using Hiker.Presenters;
using Hiker.Services;

namespace Hiker.Pages;

/// <summary>
/// Pantalla «Acerca de»: informacion de la app, contacto, apoyo, seleccion de idioma y las
/// declaraciones de privacidad, licencia y aviso legal (constitucion, anexo A.9). Los textos se
/// enlazan a <see cref="AboutPresenter"/>.
/// </summary>
public partial class AboutPage : ContentPage
{
    private AboutPresenter? _presenter;

    public AboutPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        BindingContext = _presenter ??= new AboutPresenter(
            (Handler?.MauiContext?.Services ?? IPlatformApplication.Current?.Services)?.GetService<TranslationService>(),
            AppInfo.Current.VersionString, new MauiLinkOpener());
        UpdateLanguageButtons();
    }

    // El idioma activo se resalta por estilo (relleno de marca) y el inactivo con contorno.
    private void UpdateLanguageButtons()
    {
        StyleLanguageButton(SpanishButton, _presenter!.SpanishActive);
        StyleLanguageButton(EnglishButton, !_presenter.SpanishActive);
    }

    private void StyleLanguageButton(Button button, bool active)
    {
        var key = active ? "PrimaryButton" : "OutlineButton";
        if (Resources.TryGetValue(key, out var style) || Application.Current!.Resources.TryGetValue(key, out style))
            button.Style = (Style)style;
    }

    private void OnSpanishClicked(object? sender, EventArgs e) => SetLanguage("es");

    private void OnEnglishClicked(object? sender, EventArgs e) => SetLanguage("en");

    private void SetLanguage(string languageCode)
    {
        _presenter?.SetLanguage(languageCode);
        UpdateLanguageButtons();
        // El menu lateral ya esta pintado: se le pide que se retraduzca.
        (Shell.Current as AppShell)?.ApplyTranslations();
    }

    private async void OnContactClicked(object? sender, EventArgs e) =>
        await _presenter!.ContactAsync(new ModernDialogs(this));
}
