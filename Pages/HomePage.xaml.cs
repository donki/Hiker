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
    private bool _followMode = true;   // el mapa arranca siguiendo al usuario (followUser=true en JS)
    private bool _headingUp = false;   // modo "Rumbo" (heading-up): desactivado por defecto
    private static bool _backgroundPromptsChecked = false; // avisos de bateria: una vez por sesion

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

        // Si el modo Rumbo estaba activo, reanuda la brujula al volver a la pagina.
        if (_headingUp)
            SetHeadingUp(true);

        // Avisos de bateria/segundo plano: antes se mostraban con AlertDialog nativo desde
        // MainActivity (prohibido). Ahora se piden aqui, una sola vez, con ModernDialog.
        await CheckBackgroundPermissionsAsync();

        // Comprobacion de version al arrancar (constitucion seccion 15): avisa si hay una version
        // mas reciente y propone actualizar. No bloqueante.
        var updateService = (Handler?.MauiContext?.Services ?? IPlatformApplication.Current?.Services)
            ?.GetService<UpdateService>();
        if (updateService != null)
            _ = updateService.CheckAndPromptAsync(this);
    }

    protected override async void OnDisappearing()
    {
        base.OnDisappearing();

        // La brujula solo hace falta mientras se ve el mapa: se detiene al salir para no gastar
        // bateria (se reanuda en OnAppearing si el modo Rumbo sigue activo).
        StopCompass();

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
        Title = T("GPS Tracker");
        if (!_hasLocation)
            statusLabel.Text = T("Obteniendo ubicación...");
    }

    /// <summary>Traduce una frase (la clave es la propia frase en español, seccion 8).</summary>
    private string T(string phrase) =>
        Handler?.MauiContext?.Services.GetService<TranslationService>()?.Translate(phrase) ?? phrase;

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
                    await SocShared.ModernDialog.AlertAsync(this, "Error", "No se pudo iniciar el GPS", "OK");
                }
            }
        }
        catch (Exception ex)
        {
            await SocShared.ModernDialog.AlertAsync(this, "Error", $"Error al iniciar GPS: {ex.Message}", "OK");
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

            UpdateRouteFollowing(location);
        });
    }

    private void UpdateLocationDisplay(Location location)
    {
        // Todo en UNA linea: Lat · Lon · precision · velocidad.
        var speedKmh = (location.Speed ?? 0) * 3.6; // m/s -> km/h
        statusLabel.Text =
            $"Lat {location.Latitude:F5} · Lon {location.Longitude:F5} · ±{location.Accuracy:F0} m · {speedKmh:F1} km/h";

        _hasLocation = true;
        _lastLocation = location;   // la usa el seguimiento al cargar una ruta, sin esperar al siguiente punto
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
            StopFollowing();   // si se borra el trazado, no queda ruta que seguir
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

        StartFollowing(points);
    }

    // ============ Seguir un recorrido cargado ============

    /// <summary>Distancia al trazado a partir de la cual se avisa de que te has salido.</summary>
    private const double OffRouteMeters = 50;

    private List<Location> _followRoute = new();
    private Location? _lastLocation;

    /// <summary>Metros acumulados desde el inicio hasta cada punto: evita recorrer la ruta entera
    /// en cada actualizacion de GPS para saber cuanto queda.</summary>
    private double[] _followCumulative = Array.Empty<double>();

    private void StartFollowing(List<Location> points)
    {
        _followRoute = points;
        _followCumulative = new double[points.Count];
        for (int i = 1; i < points.Count; i++)
            _followCumulative[i] = _followCumulative[i - 1] + MetersBetween(points[i - 1], points[i]);

        followBar.IsVisible = true;
        followTitleLabel.Text = T("Siguiendo la ruta");
        followDetailLabel.Text = string.Format(T("Longitud total: {0}"), FormatDistance(_followCumulative[^1]));
        followStateDot.Color = (Color)Application.Current!.Resources["Success"];

        // Si ya hay posicion, no esperar al siguiente punto GPS para decir si estas en la ruta.
        if (_lastLocation is not null)
            UpdateRouteFollowing(_lastLocation);
    }

    private void OnStopFollowingClicked(object sender, EventArgs e) => StopFollowing();

    private void StopFollowing()
    {
        _followRoute = new List<Location>();
        _followCumulative = Array.Empty<double>();
        followBar.IsVisible = false;
    }

    /// <summary>
    /// Actualiza el aviso de seguimiento: a que distancia esta el trazado y cuanto queda hasta el
    /// final. Se mide contra los vertices de la ruta, que es lo que hay guardado; con la densidad
    /// de puntos de un GPX es suficiente y no hace falta proyectar sobre cada segmento.
    /// </summary>
    private void UpdateRouteFollowing(Location current)
    {
        if (_followRoute.Count == 0)
            return;

        var nearest = 0;
        var nearestMeters = double.MaxValue;
        for (int i = 0; i < _followRoute.Count; i++)
        {
            var d = MetersBetween(current, _followRoute[i]);
            if (d < nearestMeters)
            {
                nearestMeters = d;
                nearest = i;
            }
        }

        var remaining = _followCumulative[^1] - _followCumulative[nearest];
        var offRoute = nearestMeters > OffRouteMeters;

        followTitleLabel.Text = offRoute
            ? string.Format(T("Te has salido: a {0} de la ruta"), FormatDistance(nearestMeters))
            : string.Format(T("En la ruta: a {0} del trazado"), FormatDistance(nearestMeters));

        // Lo que "queda" solo significa algo si estas sobre la ruta: fuera de ella el punto mas
        // cercano puede ser el final y saldria "quedan 0 m" estando a kilometros.
        followDetailLabel.Text = offRoute
            ? string.Format(T("Longitud total: {0}"), FormatDistance(_followCumulative[^1]))
            : string.Format(T("Quedan {0}"), FormatDistance(remaining));

        followStateDot.Color = (Color)Application.Current!.Resources[offRoute ? "Danger" : "Success"];
    }

    private static double MetersBetween(Location a, Location b) =>
        Location.CalculateDistance(a, b, DistanceUnits.Kilometers) * 1000;

    private string FormatDistance(double meters)
    {
        var ci = System.Globalization.CultureInfo.CurrentCulture;
        return meters >= 1000
            ? $"{(meters / 1000).ToString("0.0", ci)} km"
            : $"{Math.Round(meters).ToString(ci)} m";
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
            await SocShared.ModernDialog.AlertAsync(this, "Error", $"No se pudo obtener la ubicación: {ex.Message}", "OK");
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
            await SocShared.ModernDialog.AlertAsync(this, "Aviso", "No hay datos de ruta para guardar", "OK");
            return;
        }

        try
        {
            var routeName = await SocShared.ModernDialog.PromptAsync(this, "Guardar Ruta", "Nombre de la ruta:", "Guardar", "Cancelar");
            if (!string.IsNullOrWhiteSpace(routeName))
            {
                if (_routeService is null)
                {
                    await SocShared.ModernDialog.AlertAsync(this, "Error", "El servicio de rutas no está disponible.", "OK");
                    return;
                }

                // Guardado real: escribe la ruta como GPX en el almacenamiento de la app.
                await _routeService.SaveRouteAsync(_recordedLocations.ToList(), routeName);
                await SocShared.ModernDialog.AlertAsync(this, "Éxito", $"Ruta '{routeName}' guardada correctamente", "OK");
            }
        }
        catch (Exception ex)
        {
            await SocShared.ModernDialog.AlertAsync(this, "Error", $"Error al guardar la ruta: {ex.Message}", "OK");
        }
    }

    private async void OnClearClicked(object sender, EventArgs e)
    {
        _recordedLocations.Clear();
        await ClearMapRoute();
    }

    /// <summary>Activa/desactiva el modo "Seguir": recentrar el mapa en cada punto GPS.</summary>
    public async void SetFollow(bool enabled)
    {
        _followMode = enabled;
        try
        {
            if (_mapReady)
                await mapWebView.EvaluateJavaScriptAsync($"setFollow({(enabled ? "true" : "false")});");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error en setFollow: {ex.Message}");
        }
    }

    /// <summary>Modo "Rumbo" (heading-up): rota el mapa segun la brujula del dispositivo.</summary>
    public async void SetHeadingUp(bool enabled)
    {
        _headingUp = enabled;
        try
        {
            if (_mapReady)
                await mapWebView.EvaluateJavaScriptAsync($"setHeadingUp({(enabled ? "true" : "false")});");

            if (enabled)
            {
                if (Compass.Default.IsSupported && !Compass.Default.IsMonitoring)
                {
                    Compass.Default.ReadingChanged += OnCompassReadingChanged;
                    Compass.Default.Start(SensorSpeed.UI);
                }
            }
            else
            {
                StopCompass();
            }
        }
        catch (FeatureNotSupportedException)
        {
            // El dispositivo no tiene brujula: se ignora, el mapa sigue en norte-arriba.
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error en modo Rumbo: {ex.Message}");
        }
    }

    private void StopCompass()
    {
        try
        {
            if (Compass.Default.IsSupported && Compass.Default.IsMonitoring)
            {
                Compass.Default.Stop();
                Compass.Default.ReadingChanged -= OnCompassReadingChanged;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error deteniendo brújula: {ex.Message}");
        }
    }

    private void OnCompassReadingChanged(object? sender, CompassChangedEventArgs e)
    {
        if (!_headingUp || !_mapReady)
            return;

        var heading = e.Reading.HeadingMagneticNorth;
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                await mapWebView.EvaluateJavaScriptAsync(
                    $"setHeading({heading.ToString(System.Globalization.CultureInfo.InvariantCulture)});");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error aplicando rumbo: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// Avisos de optimización de batería y ejecución en segundo plano. Se muestran con el
    /// diálogo NO nativo (ModernDialog) desde la capa MAUI (la constitución prohíbe los
    /// AlertDialog nativos). Solo se pregunta una vez (Preferences) y solo en Android.
    /// </summary>
    private async Task CheckBackgroundPermissionsAsync()
    {
        if (_backgroundPromptsChecked)
            return;
        _backgroundPromptsChecked = true;

#if ANDROID
        try
        {
            if (MainActivity.IsBatteryOptimizationIgnored())
                return; // ya esta exenta: no hace falta preguntar nada

            var translation = Handler?.MauiContext?.Services.GetService<TranslationService>();
            string L(string phrase) => translation?.Translate(phrase) ?? phrase;

            if (!Preferences.Get("prompt_battery_opt", false))
            {
                Preferences.Set("prompt_battery_opt", true);
                bool ok = await SocShared.ModernDialog.AlertAsync(this,
                    L("Optimización de batería"),
                    L("Para mejorar el posicionamiento y el rendimiento de Hiker, permite que la aplicación funcione sin restricciones de batería. ¿Deseas modificar esta configuración ahora?"),
                    L("Sí"), L("No"));
                if (ok) MainActivity.OpenBatteryOptimizationSettings();
            }

            if (!Preferences.Get("prompt_background_exec", false))
            {
                Preferences.Set("prompt_background_exec", true);
                bool ok = await SocShared.ModernDialog.AlertAsync(this,
                    L("Ejecución en segundo plano"),
                    L("Para que Hiker funcione correctamente en segundo plano, permite la ejecución sin restricciones. ¿Deseas modificar esta configuración ahora?"),
                    L("Sí"), L("No"));
                if (ok) MainActivity.OpenBatterySaverSettings();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error comprobando permisos de segundo plano: {ex.Message}");
        }
#else
        await Task.CompletedTask;
#endif
    }
}