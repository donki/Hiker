using Hiker.Services;
using System.Collections.ObjectModel;

namespace Hiker.Pages;

public partial class HomePage : ContentPage
{
    private GeolocationService? _geolocationService;
    private GpsFilterService? _gpsFilterService;
    private SettingsService? _settingsService;
    private RouteService? _routeService;
    private readonly ObservableCollection<Location> _recordedLocations = new();
    private bool _isTracking = false;
    private bool _mapReady = false;
    private bool _hasLocation = false;

    public HomePage()
    {
        InitializeComponent();

        // Configurar mapa inicial
        InitializeMap();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Textos de UI externalizados (constitucion seccion 8): titulo, panel GPS y botones.
        TranslateUi();

        // Resolver servicios de forma fiable: el proveedor global (IPlatformApplication.Current)
        // siempre esta disponible; antes se dependia solo de Handler.MauiContext.Services, que
        // podia ser null al aparecer la pagina y dejaba la geolocalizacion sin arrancar.
        var services = Handler?.MauiContext?.Services
                       ?? IPlatformApplication.Current?.Services;
        GeolocationService.LogInfo($"HomePage.OnAppearing services={(services != null)}");
        if (services != null)
        {
            _geolocationService ??= services.GetService<GeolocationService>();
            _gpsFilterService ??= services.GetService<GpsFilterService>();
            _settingsService ??= services.GetService<SettingsService>();
            _routeService ??= services.GetService<RouteService>();

            GeolocationService.LogInfo($"HomePage.OnAppearing geoService={(_geolocationService != null)}");
            if (_geolocationService != null)
            {
                _geolocationService.OnLocationChangedDelegate -= OnLocationChanged;
                _geolocationService.OnLocationChangedDelegate += OnLocationChanged;
                await StartLocationUpdates();
                // Centrado inicial rapido: evita que el mapa se quede en su centro por defecto
                // esperando al primer fix del GPS (antes parecia "posicionar" en Madrid).
                await CenterOnInitialLocationAsync();
            }
        }

        // Si venimos de la pantalla de Rutas con una ruta seleccionada, la pintamos en el mapa.
        await LoadPendingRouteAsync();
    }

    protected override async void OnDisappearing()
    {
        base.OnDisappearing();

        // Si se esta grabando una ruta, NO se detiene el GPS al cambiar de pestaña: antes, salir
        // de esta pagina cortaba la captura y podia perderse la traza en curso.
        if (_isTracking)
            return;

        if (_geolocationService != null)
        {
            await _geolocationService.ListeningStopAsync();
        }
    }

    /// <summary>
    /// Traduce los textos estaticos de la interfaz con TranslationService (clave = frase en
    /// español). Los valores dinamicos de ubicacion se sobreescriben luego con datos reales.
    /// </summary>
    private void TranslateUi()
    {
        var translation = Handler?.MauiContext?.Services.GetService<TranslationService>();
        string L(string phrase) => translation?.Translate(phrase) ?? phrase;

        Title = L("GPS Tracker");
        if (!_hasLocation)
            statusLabel.Text = L("Obteniendo ubicación...");
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

    /// <summary>
    /// Centra el mapa cuanto antes: primero con la ultima ubicacion conocida (instantanea) y
    /// luego con un fix fresco del GPS. Asi la pantalla no se queda en el centro por defecto
    /// del mapa mientras llega el primer punto.
    /// </summary>
    private async Task CenterOnInitialLocationAsync()
    {
        if (_geolocationService == null)
            return;

        try
        {
            // La ETIQUETA de ubicacion se actualiza en cuanto hay un fix, SIN esperar al mapa
            // (antes se bloqueaba aqui esperando al WebView y se quedaba en "Obteniendo ubicacion").
            // El centrado del mapa se hace aparte, cuando el WebView este listo.
            var last = await _geolocationService.GetLastKnownLocationAsync();
            if (last != null)
            {
                UpdateLocationDisplay(last);
                await CenterMapWhenReadyAsync(last, 15);
            }

            var current = await _geolocationService.GetCurrentLocationAsync();
            if (current != null)
            {
                UpdateLocationDisplay(current);
                await CenterMapWhenReadyAsync(current, 16);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error centrando ubicacion inicial: {ex.Message}");
        }
    }

    private async Task CenterMapAsync(Location location, int zoom)
    {
        var ci = System.Globalization.CultureInfo.InvariantCulture;
        await mapWebView.EvaluateJavaScriptAsync(
            $"centerOnLocation({location.Latitude.ToString(ci)}, {location.Longitude.ToString(ci)}, {zoom});");
    }

    /// <summary>Centra el mapa cuando el WebView este listo (espera hasta ~3s). No bloquea la
    /// actualizacion de la etiqueta de ubicacion, que ocurre por separado.</summary>
    private async Task CenterMapWhenReadyAsync(Location location, int zoom)
    {
        for (int i = 0; i < 20 && !_mapReady; i++)
            await Task.Delay(150);
        if (_mapReady)
            await CenterMapAsync(location, zoom);
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
        // Todo en UNA linea: Lat · Lon · precision · velocidad.
        var speedKmh = (location.Speed ?? 0) * 3.6; // m/s -> km/h
        statusLabel.Text =
            $"Lat {location.Latitude:F5} · Lon {location.Longitude:F5} · ±{location.Accuracy:F0} m · {speedKmh:F1} km/h";

        _hasLocation = true;
        UpdateStateIcon();
    }

    /// <summary>Icono de estado en la barra: grabando (record rojo), localizado (play) o en
    /// espera (pausa), segun si se esta grabando ruta y si ya hay ubicacion.</summary>
    private void UpdateStateIcon()
    {
        stateIcon.Source = _isTracking
            ? "ic_st_rec.png"
            : (_hasLocation ? "ic_st_play.png" : "ic_st_pause.png");
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

    /// <summary>Dibuja una ruta guardada completa en el mapa y encuadra para verla entera.</summary>
    private async Task DrawSavedRouteAsync(List<Location> points)
    {
        if (!_mapReady || points.Count == 0)
            return;

        var ci = System.Globalization.CultureInfo.InvariantCulture;
        var coords = string.Join(",", points.Select(p =>
            $"[{p.Latitude.ToString(ci)},{p.Longitude.ToString(ci)}]"));
        await mapWebView.EvaluateJavaScriptAsync($"drawRoute([{coords}]);");
    }

    /// <summary>Si la pantalla de Rutas pidió abrir una ruta, la carga y la pinta.</summary>
    private async Task LoadPendingRouteAsync()
    {
        var name = RouteService.PendingRouteToLoad;
        if (string.IsNullOrEmpty(name) || _routeService is null)
            return;

        RouteService.PendingRouteToLoad = null;
        try
        {
            var points = await _routeService.LoadRouteLocationsAsync(name);
            await DrawSavedRouteAsync(points);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading pending route: {ex.Message}");
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

    /// <summary>Invoca una accion del mapa desde el submenu del menu hamburguesa
    /// (play=iniciar, stop=parar, save=guardar, clear=borrar).</summary>
    public void RunMapAction(string action)
    {
        switch (action)
        {
            case "play": OnStartTrackingClicked(this, EventArgs.Empty); break;
            case "stop": OnStopTrackingClicked(this, EventArgs.Empty); break;
            case "save": OnSaveRouteClicked(this, EventArgs.Empty); break;
            case "clear": OnClearClicked(this, EventArgs.Empty); break;
        }
    }

    private async void OnStartTrackingClicked(object sender, EventArgs e)
    {
        if (_isTracking)
            return;

        _isTracking = true;
        _recordedLocations.Clear();

        // Se reinicia el filtro para no arrastrar el estado del Kalman de una grabacion anterior,
        // que sesgaba los primeros puntos de la nueva ruta.
        _gpsFilterService?.Reset();

        UpdateStateIcon();

        // Limpiar mapa
        await ClearMapRoute();
    }

    private void OnStopTrackingClicked(object sender, EventArgs e)
    {
        _isTracking = false;
        UpdateStateIcon();
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
                if (_routeService is null)
                {
                    await DisplayAlert("Error", "El servicio de rutas no está disponible.", "OK");
                    return;
                }

                // Guardado real: escribe la ruta como GPX en el almacenamiento de la app.
                await _routeService.SaveRouteAsync(_recordedLocations.ToList(), routeName);
                await DisplayAlert("Éxito", $"Ruta '{routeName}' guardada correctamente", "OK");
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
    }
}