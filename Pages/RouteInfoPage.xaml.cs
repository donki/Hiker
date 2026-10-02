using Hiker.Presenters;
using Hiker.Services;

namespace Hiker.Pages;

/// <summary>Ficha de una ruta. Las etiquetas se enlazan a <see cref="RouteInfoPresenter"/>.</summary>
public partial class RouteInfoPage : ContentPage
{
    private readonly RouteInfoPresenter _presenter;

    public RouteInfoPage(string routeName, RouteService routeService, TranslationService? translations)
    {
        InitializeComponent();
        _presenter = new RouteInfoPresenter(routeName, routeService, p => translations?.Translate(p) ?? p,
            new ModernDialogs(this), new ShellNavigator(this, _ => this));
        profileView.Drawable = _presenter.Profile;
        BindingContext = _presenter;
        Loaded += async (_, _) =>
        {
            if (await _presenter.LoadAsync(Application.Current?.RequestedTheme == AppTheme.Dark))
                profileView.Invalidate();
        };
    }

    private async void OnShowOnMapClicked(object? sender, EventArgs e) => await _presenter.ShowOnMapAsync();
}
