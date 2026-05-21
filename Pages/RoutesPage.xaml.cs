using Hiker.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Hiker.Pages;

public partial class RoutesPage : ContentPage
{
    private RouteService? _routeService;
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
        }
        
        await LoadRoutes();
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
                    Distance = CalculateDistance(route.locations),
                    CreatedDate = DateTime.Now, // Aquí deberías usar la fecha real de la ruta
                    Route = route
                });
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Error cargando rutas: {ex.Message}", "OK");
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
            // Navegar a la página principal y cargar la ruta
            await Shell.Current.GoToAsync("//GPS");
            // Aquí implementarías la lógica para mostrar la ruta en el mapa
            await DisplayAlert("Información", $"Ruta '{routeInfo.Name}' cargada", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Error cargando ruta: {ex.Message}", "OK");
        }
    }

    private async void OnDeleteRoute(RouteInfo routeInfo)
    {
        try
        {
            var confirm = await DisplayAlert("Confirmar", 
                $"¿Eliminar la ruta '{routeInfo.Name}'?", "Sí", "No");
            
            if (confirm && _routeService != null)
            {
                await _routeService.DeleteRouteAsync(routeInfo.Route.routeName);
                Routes.Remove(routeInfo);
                await DisplayAlert("Éxito", "Ruta eliminada correctamente", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Error eliminando ruta: {ex.Message}", "OK");
        }
    }

    private async void OnLoadGpxClicked(object sender, EventArgs e)
    {
        try
        {
            var result = await FilePicker.PickAsync(new PickOptions
            {
                PickerTitle = "Seleccionar archivo GPX",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.Android, new[] { "application/gpx+xml", "text/xml" } },
                    { DevicePlatform.WinUI, new[] { ".gpx", ".xml" } }
                })
            });

            if (result != null)
            {
                using var stream = await result.OpenReadAsync();
                using var reader = new StreamReader(stream);
                var gpxContent = await reader.ReadToEndAsync();
                
                // Aquí implementarías la lógica para procesar el GPX
                await DisplayAlert("Éxito", $"Archivo GPX '{result.FileName}' cargado", "OK");
                await LoadRoutes();
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Error cargando GPX: {ex.Message}", "OK");
        }
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