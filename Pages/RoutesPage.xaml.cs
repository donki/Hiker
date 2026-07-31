using Hiker.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Hiker.Pages;

public partial class RoutesPage : ContentPage
{
    private RouteService? _routeService;
    private TranslationService? _translationService;
    public ObservableCollection<RouteInfo> Routes { get; set; } = new();
    
    public ICommand LoadRouteCommand { get; }
    public ICommand DeleteRouteCommand { get; }

    public RoutesPage()
    {
        InitializeComponent();

        LoadRouteCommand = new Command<RouteInfo>(OnLoadRoute);
        DeleteRouteCommand = new Command<RouteInfo>(OnDeleteRoute);
        
        routesCollectionView.ItemsSource = Routes;
        BindingContext = this;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        // Obtener servicios cuando el Handler esté disponible
        if (Handler?.MauiContext?.Services != null)
        {
            _routeService = Handler.MauiContext.Services.GetService<RouteService>();
            _translationService = Handler.MauiContext.Services.GetService<TranslationService>();
        }

        TranslateUi();

        await LoadRoutes();
    }

    /// <summary>Textos estaticos externalizados (constitucion seccion 8).</summary>
    private void TranslateUi()
    {
        string L(string phrase) => _translationService?.Translate(phrase) ?? phrase;

        Title = L("Rutas Guardadas");
        headerLabel.Text = L("Gestión de Rutas");
        loadGpxButton.Text = L("Cargar GPX");
        refreshButton.Text = L("Actualizar");
    }

    private async Task LoadRoutes()
    {
        try
        {
            Routes.Clear();
            if (_routeService == null) return;
            
            var routes = await _routeService.GetAllRoutesAsync();

            foreach (var route in routes)
            {
                Routes.Add(new RouteInfo
                {
                    Name = route.routeName,
                    Distance = route.totalDistance, // ProcessData ya calcula la distancia en km
                    CreatedDate = _routeService.GetRouteDate(route.routeName),
                    Route = route
                });
            }
        }
        catch (Exception ex)
        {
            await SocShared.ModernDialog.AlertAsync(this, "Error", $"Error cargando rutas: {ex.Message}", "OK");
        }
    }

    private double CalculateDistance(List<Location> locations)
    {
        if (locations.Count < 2) return 0;
        
        double totalDistance = 0;
        for (int i = 1; i < locations.Count; i++)
        {
            totalDistance += Location.CalculateDistance(
                locations[i-1].Latitude, locations[i-1].Longitude,
                locations[i].Latitude, locations[i].Longitude,
                DistanceUnits.Kilometers);
        }
        
        return totalDistance;
    }

    private async void OnLoadRoute(RouteInfo routeInfo)
    {
        try
        {
            // Se marca la ruta a dibujar y se salta al mapa; HomePage la carga al aparecer.
            RouteService.PendingRouteToLoad = routeInfo.Name;
            await Shell.Current.GoToAsync("//GPS");
        }
        catch (Exception ex)
        {
            await SocShared.ModernDialog.AlertAsync(this, "Error", $"Error cargando ruta: {ex.Message}", "OK");
        }
    }

    private async void OnDeleteRoute(RouteInfo routeInfo)
    {
        try
        {
            var confirm = await SocShared.ModernDialog.AlertAsync(this, "Confirmar",
                $"¿Eliminar la ruta '{routeInfo.Name}'?", "Sí", "No");

            if (confirm && _routeService != null)
            {
                await _routeService.DeleteRouteAsync(routeInfo.Name);
                Routes.Remove(routeInfo);
            }
        }
        catch (Exception ex)
        {
            await SocShared.ModernDialog.AlertAsync(this, "Error", $"Error eliminando ruta: {ex.Message}", "OK");
        }
    }

    private async void OnLoadGpxClicked(object sender, EventArgs e)
    {
        try
        {
            if (_routeService is null)
                return;

            var result = await FilePicker.PickAsync(new PickOptions
            {
                PickerTitle = "Seleccionar archivo GPX",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    // Android resuelve .gpx como octet-stream/xml (no hay MIME oficial); con "*/*"
                    // el selector muestra los .gpx en vez de dejarlos grises. Se valida al parsear.
                    { DevicePlatform.Android, new[] { "*/*" } },
                    { DevicePlatform.WinUI, new[] { ".gpx", ".xml" } }
                })
            });

            if (result != null)
            {
                using var stream = await result.OpenReadAsync();
                using var reader = new StreamReader(stream);
                var gpxContent = await reader.ReadToEndAsync();

                // Importar = parsear el GPX y guardarlo como una ruta mas de la app, con el nombre
                // del fichero. Asi aparece en la lista y se puede abrir en el mapa como el resto.
                var name = Path.GetFileNameWithoutExtension(result.FileName);
                var data = await _routeService.SetRoute(gpxContent);
                var points = await ExtractLocations(gpxContent);
                if (points.Count == 0)
                {
                    await SocShared.ModernDialog.AlertAsync(this, "Aviso", "El archivo GPX no contiene puntos.", "OK");
                    return;
                }

                await _routeService.SaveRouteAsync(points, name);
                await LoadRoutes();
                await SocShared.ModernDialog.AlertAsync(this, "Éxito", $"Ruta '{name}' importada.", "OK");
            }
        }
        catch (Exception ex)
        {
            await SocShared.ModernDialog.AlertAsync(this, "Error", $"Error cargando GPX: {ex.Message}", "OK");
        }
    }

    private static async Task<List<Location>> ExtractLocations(string gpxContent)
    {
        var points = new List<Location>();
        await Task.Run(() =>
        {
            var gpx = Helpers.GPXFileHelper.FromXML(gpxContent);
            foreach (var seg in gpx.Tracks.SelectMany(t => t.Segments))
                foreach (var p in seg.TrackPoints)
                    points.Add(new Location((double)p.Latitude, (double)p.Longitude, new DateTimeOffset(p.Time))
                    {
                        Altitude = (double)p.Elevation
                    });
        });
        return points;
    }

    private async void OnRefreshClicked(object sender, EventArgs e)
    {
        await LoadRoutes();
    }
}

public class RouteInfo
{
    public string Name { get; set; } = "";
    public double Distance { get; set; }
    public DateTime CreatedDate { get; set; }
    public dynamic Route { get; set; } = null!;
}