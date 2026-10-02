using Hiker.Presenters;
using Hiker.Services;

namespace Hiker.Pages;

/// <summary>Configuracion: solo el idioma. Los textos se enlazan a <see cref="LanguagePresenter"/>.</summary>
public partial class SettingsPage : ContentPage
{
    // Verde de marca de Hiker para resaltar el idioma activo.
    private static readonly Color ActiveLanguage = Color.FromArgb("#2E7D32");

    private LanguagePresenter? _presenter;

    public SettingsPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // La primera vez que aparece la pagina el Handler aun no tiene MauiContext: sin la caida al
        // proveedor global, la pantalla salia siempre en castellano y los botones no hacian nada.
        BindingContext = _presenter ??= new LanguagePresenter(
            (Handler?.MauiContext?.Services ?? IPlatformApplication.Current?.Services)?.GetService<TranslationService>());
        UpdateLanguageButtons();
    }

    private void UpdateLanguageButtons()
    {
        StyleLanguageButton(spanishButton, _presenter!.SpanishActive);
        StyleLanguageButton(englishButton, !_presenter.SpanishActive);
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
        _presenter?.SetLanguage(languageCode);
        UpdateLanguageButtons();
        // El menu lateral ya esta pintado: se le pide que se retraduzca.
        (Shell.Current as AppShell)?.ApplyTranslations();
    }
}
