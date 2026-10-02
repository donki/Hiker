using Hiker.Presenters;
using Hiker.Services;

namespace Hiker.Pages;

/// <summary>
/// Pantalla del mapa. Solo pinta: la logica (ubicacion, grabar, guardar, ajustar al mapa, seguir
/// una ruta, Rumbo, avisos de bateria) esta en <see cref="HomePresenter"/>, que se prueba aparte.
/// </summary>
public partial class HomePage : ContentPage, IHomeView, IMapBridge
{
    private readonly HomePresenter _presenter;
    private IDispatcherTimer? _recordTimer;

    public HomePage()
    {
        InitializeComponent();
        _presenter = new HomePresenter(this, this, new ModernDialogs(this), new MauiUiThread(), L,
            () => FileSystem.OpenAppPackageFileAsync("map.html"));
        _ = _presenter.InitializeMapAsync();
    }

    /// <summary>Pregunta abierta cuyo cierre tiraria la ruta: atras no la cierra (ver AppShell).</summary>
    public bool IsKeepOrDiscardOpen => _presenter.IsKeepOrDiscardOpen;

    private static IServiceProvider? Services => IPlatformApplication.Current?.Services;

    /// <summary>
    /// Traduce una frase (la clave es la propia frase en castellano, seccion 8). Se cae al
    /// proveedor global cuando el Handler aun no esta montado.
    /// </summary>
    private string L(string phrase) =>
        (Handler?.MauiContext?.Services ?? Services)?.GetService<TranslationService>()?.Translate(phrase) ?? phrase;

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _presenter.OnAppearingAsync(HomeServices.From(Handler?.MauiContext?.Services ?? Services));
    }

    protected override async void OnDisappearing()
    {
        base.OnDisappearing();
        await _presenter.OnDisappearingAsync();
    }

    // Lo que llega del menu lateral (AppShell).
    public void RunMapAction(string action) => _ = _presenter.RunMapActionAsync(action);
    public void SetFollow(bool enabled) => _ = _presenter.SetFollowAsync(enabled);
    public void SetHeadingUp(bool enabled) => _ = _presenter.SetHeadingUpAsync(enabled);

    private async void OnGpsButtonClicked(object sender, EventArgs e) => await _presenter.CenterOnMeAsync();
    private async void OnStartTrackingClicked(object sender, EventArgs e) => await _presenter.StartTrackingAsync();
    private async void OnStopTrackingClicked(object sender, EventArgs e) => await _presenter.StopTrackingAsync();
    private void OnStopFollowingClicked(object sender, EventArgs e) => _presenter.StopFollowing();
    private async void OnCompareSwapClicked(object sender, EventArgs e) => await _presenter.SwapComparisonAsync();
    private void OnCompareSaveClicked(object sender, EventArgs e) => _presenter.ChooseComparison();

    // ---- IMapBridge ----
    public Task<string?> EvaluateAsync(string script) => mapWebView.EvaluateJavaScriptAsync(script);

    // ---- IHomeView ----
    public void SetTitle(string text) => Title = text;
    public void SetStatus(string text) => statusLabel.Text = text;
    public void SetStateIcon(string file) => stateIcon.Source = file;
    public void SetMapHtml(string html) => mapWebView.Source = new HtmlWebViewSource { Html = html };

    public void ShowRecording(bool recording)
    {
        recordButton.IsVisible = !recording;
        recordBar.IsVisible = recording;
        if (!recording)
        {
            _recordTimer?.Stop();
            return;
        }

        if (_recordTimer is null)
        {
            _recordTimer = Dispatcher.CreateTimer();
            _recordTimer.Interval = TimeSpan.FromSeconds(1);
            _recordTimer.Tick += (_, _) => _presenter.UpdateRecordingLabels();
        }
        _recordTimer.Start();
    }

    public void SetRecordingLabels(string title, string detail)
    {
        recordTitleLabel.Text = title;
        recordDetailLabel.Text = detail;
    }

    public void ShowFollowBar(bool visible) => followBar.IsVisible = visible;

    public void SetFollowStatus(string title, string detail, bool offRoute)
    {
        followTitleLabel.Text = title;
        followDetailLabel.Text = detail;
        followStateDot.Color = (Color)Application.Current!.Resources[offRoute ? "Danger" : "Success"];
    }

    public void ShowCompareBar(bool visible, bool withButtons)
    {
        compareSwapButton.IsVisible = withButtons;
        compareSaveButton.IsVisible = withButtons;
        compareBar.IsVisible = visible;
    }

    public void SetCompareTexts(string title, string detail)
    {
        compareTitleLabel.Text = title;
        compareDetailLabel.Text = detail;
    }
}
