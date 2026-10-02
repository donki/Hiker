using Hiker.Presenters;
using Hiker.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Hiker.Pages;

/// <summary>Pantalla de Rutas. Solo pinta: la logica esta en <see cref="RoutesPresenter"/>.</summary>
public partial class RoutesPage : ContentPage
{
    private readonly RoutesPresenter _presenter;

    public ObservableCollection<RouteInfo> Routes => _presenter.Routes;
    public ICommand LoadRouteCommand { get; }
    public ICommand DeleteRouteCommand { get; }
    public ICommand InfoRouteCommand { get; }

    public RoutesPage()
    {
        InitializeComponent();

        _presenter = new RoutesPresenter(() => Resolve<RouteService>(), L, new ModernDialogs(this),
            new ShellNavigator(this, name => new RouteInfoPage(name, Resolve<RouteService>()!, Resolve<TranslationService>())),
            new MauiGpxPicker());

        LoadRouteCommand = new Command<RouteInfo>(async r => await _presenter.OpenOnMapAsync(r));
        DeleteRouteCommand = new Command<RouteInfo>(async r => await _presenter.DeleteAsync(r));
        InfoRouteCommand = new Command<RouteInfo>(async r => await _presenter.ShowInfoAsync(r));

        routesCollectionView.ItemsSource = Routes;
        BindingContext = this;
    }

    /// <summary>Se cae a <c>IPlatformApplication</c> cuando el Handler todavia no esta montado.</summary>
    private T? Resolve<T>() where T : class =>
        (Handler?.MauiContext?.Services ?? IPlatformApplication.Current?.Services)?.GetService<T>();

    private string L(string phrase) => Resolve<TranslationService>()?.Translate(phrase) ?? phrase;

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        (Title, headerLabel.Text, loadGpxButton.Text, refreshButton.Text) = _presenter.Texts;
        await _presenter.LoadAsync();
    }

    private async void OnLoadGpxClicked(object sender, EventArgs e) => await _presenter.ImportGpxAsync();

    private async void OnRefreshClicked(object sender, EventArgs e) => await _presenter.LoadAsync();
}
