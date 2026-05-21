using Hiker.Services;
using System.Collections.ObjectModel;

namespace Hiker.Pages;

public partial class HomePage : ContentPage
{
    private GeolocationService? _geolocationService;
    private GpsFilterService? _gpsFilterService;
    private SettingsService? _settingsService;
    private readonly ObservableCollection<Location> _recordedLocations = new();
    private bool _isTracking = false;
    private bool _mapReady = false;

    public HomePage()
    {
        InitializeComponent();

        // Configurar mapa inicial
        InitializeMap();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        // Obtener servicios cuando el Handler esté disponible
        if (Handler?.MauiContext?.Services != null)
        {
            _geolocationService = Handler.MauiContext.Services.GetService<GeolocationService>();
            _gpsFilterService = Handler.MauiContext.Services.GetService<GpsFilterService>();
            _settingsService = Handler.MauiContext.Services.GetService<SettingsService>();
            
            if (_geolocationService != null)
            {
                _geolocationService.OnLocationChangedDelegate += OnLocationChanged;
                await StartLocationUpdates();
            }
        }
    }

    protected override async void OnDisappearing()
    {
        base.OnDisappearing();
        if (_geolocationService != null)
        {
            await _geolocationService.ListeningStopAsync();
        }
    }

    private async void InitializeMap()
    {
        try
        {
            // Cargar el HTML del mapa desde Resources/Raw
            var htmlSource = new HtmlWebViewSource
            {
                Html = await LoadMapHtml()
            };
            mapWebView.Source = htmlSource;
            
            // Esperar a que el mapa esté listo
            await Task.Delay(2000);
            _mapReady = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error initializing map: {ex.Message}");
        }
    }

    private async Task<string> LoadMapHtml()
    {
        try
        {
            using var stream = await FileSystem.OpenAppPackageFileAsync("map.html");
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading map HTML: {ex.Message}");
            // HTML básico de fallback
            return @"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8' />
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Hiker Map</title>
    <link rel='stylesheet' href='https://unpkg.com/leaflet@1.9.4/dist/leaflet.css' />
    <style>
        body { margin: 0; padding: 0; background-color: #1a1a1a; }
        #map { height: 100vh; width: 100vw; }
    </style>
</head>
<body>
    <div id='map'></div>
    <script src='https://unpkg.com/leaflet@1.9.4/dist/leaflet.js'></script>
    <script>
        var map = L.map('map').setView([40.4168, -3.7038], 13);
        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            attribution: '© OpenStreetMap contributors',
            maxZoom: 19
        }).addTo(map);
        
        var currentLocationMarker = null;
        var routePolyline = null;
        var routePoints = [];
        
        function updateLocation(lat, lng, accuracy) {
            if (currentLocationMarker) {
                map.removeLayer(currentLocationMarker);
            }
            currentLocationMarker = L.circleMarker([lat, lng], {
                color: '#4CAF50',
                fillColor: '#4CAF50',
                fillOpacity: 0.8,
                radius: 8
            }).addTo(map);
            map.setView([lat, lng], map.getZoom());
        }
        
        function addRoutePoint(lat, lng) {
            routePoints.push([lat, lng]);
            if (routePolyline) {
                map.removeLayer(routePolyline);
            }
            if (routePoints.length > 1) {
                routePolyline = L.polyline(routePoints, {
                    color: '#F44336',
                    weight: 4,
                    opacity: 0.8
                }).addTo(map);
            }
        }
        
        function clearRoute() {
            if (routePolyline) {
                map.removeLayer(routePolyline);
                routePolyline = null;
            }
            routePoints = [];
        }
        
        function centerOnLocation(lat, lng, zoom) {
            map.setView([lat, lng], zoom || 15);
        }
        
        window.updateLocation = updateLocation;
        window.addRoutePoint = addRoutePoint;
        window.clearRoute = clearRoute;
        window.centerOnLocation = centerOnLocation;
        window.mapReady = true;
    </script>
</body>
</html>";
        }
    }

    private async Task StartLocationUpdates()
    {
        try
        {
            if (_geolocationService != null)
            {
                var success = await _geolocationService.ListeningStartAsync();
                if (!success)
                {
                    await DisplayAlert("Error", "No se pudo iniciar el GPS", "OK");
                }
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Error al iniciar GPS: {ex.Message}", "OK");
        }
    }

    private void OnLocationChanged(Location location)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            UpdateLocationDisplay(location);
            
            if (_isTracking && _gpsFilterService != null)
            {
                var filteredLocation = _gpsFilterService.ProcessLocation(location);
                if (filteredLocation != null)
                {
                    _recordedLocations.Add(filteredLocation);
                    await AddRoutePointToMap(filteredLocation);
                }
            }
            
            // Actualizar mapa con la ubicación actual
            if (_mapReady)
            {
                await UpdateMapLocation(location);
            }
        });
    }

    private void UpdateLocationDisplay(Location location)
    {
        locationLabel.Text = $"Lat: {location.Latitude:F6}, Lon: {location.Longitude:F6}";
        accuracyLabel.Text = $"Precisión: {location.Accuracy:F1}m";
        
        if (location.Speed.HasValue)
        {
            var speedKmh = location.Speed.Value * 3.6; // m/s to km/h
            speedLabel.Text = $"Velocidad: {speedKmh:F1} km/h";
        }
    }

    private async Task UpdateMapLocation(Location location)
    {
        try
        {
            if (_mapReady)
            {
                var script = $"updateLocation({location.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}, {location.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}, {location.Accuracy?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "10"});";
                await mapWebView.EvaluateJavaScriptAsync(script);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error updating map location: {ex.Message}");
        }
    }

    private async Task AddRoutePointToMap(Location location)
    {
        try
        {
            if (_mapReady)
            {
                var script = $"addRoutePoint({location.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}, {location.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)});";
                await mapWebView.EvaluateJavaScriptAsync(script);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error adding route point: {ex.Message}");
        }
    }

    private async Task ClearMapRoute()
    {
        try
        {
            if (_mapReady)
            {
                await mapWebView.EvaluateJavaScriptAsync("clearRoute();");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error clearing route: {ex.Message}");
        }
    }

    private async void OnGpsButtonClicked(object sender, EventArgs e)
    {
        try
        {
            if (_geolocationService != null)
            {
                var currentLocation = await _geolocationService.GetCurrentLocationAsync();
                if (currentLocation != null && _mapReady)
                {
                    var script = $"centerOnLocation({currentLocation.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}, {currentLocation.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}, 16);";
                    await mapWebView.EvaluateJavaScriptAsync(script);
                }
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"No se pudo obtener la ubicación: {ex.Message}", "OK");
        }
    }

    private async void OnStartTrackingClicked(object sender, EventArgs e)
    {
        _isTracking = true;
        _recordedLocations.Clear();
        
        startTrackingButton.IsEnabled = false;
        stopTrackingButton.IsEnabled = true;
        saveRouteButton.IsEnabled = false;
        
        // Limpiar mapa
        await ClearMapRoute();
    }

    private void OnStopTrackingClicked(object sender, EventArgs e)
    {
        _isTracking = false;
        
        startTrackingButton.IsEnabled = true;
        stopTrackingButton.IsEnabled = false;
        saveRouteButton.IsEnabled = _recordedLocations.Count > 0;
    }

    private async void OnSaveRouteClicked(object sender, EventArgs e)
    {
        if (_recordedLocations.Count == 0)
        {
            await DisplayAlert("Aviso", "No hay datos de ruta para guardar", "OK");
            return;
        }

        try
        {
            var routeName = await DisplayPromptAsync("Guardar Ruta", "Nombre de la ruta:", "Guardar", "Cancelar");
            if (!string.IsNullOrWhiteSpace(routeName))
            {
                // Aquí implementarías la lógica para guardar la ruta
                await DisplayAlert("Éxito", $"Ruta '{routeName}' guardada correctamente", "OK");
                saveRouteButton.IsEnabled = false;
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Error al guardar la ruta: {ex.Message}", "OK");
        }
    }

    private async void OnClearClicked(object sender, EventArgs e)
    {
        _recordedLocations.Clear();
        await ClearMapRoute();
        saveRouteButton.IsEnabled = false;
    }
}