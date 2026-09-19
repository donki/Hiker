using Hiker.Services;

namespace Hiker.Pages;

public partial class HomePage : ContentPage
{
    private GeolocationService? _geolocationService;
    private GpsFilterService? _gpsFilterService;
    private SettingsService? _settingsService;
    private RouteService? _routeService;
    private MapMatchService? _mapMatch;
    /// <summary>
    /// La grabacion vive fuera de la pagina (ver <see cref="TrackRecorder"/>): quien recibe los
    /// puntos es el servicio en primer plano, que sigue vivo con la pantalla apagada.
    /// </summary>
    private TrackRecorder? _recorder;

    private bool _isTracking => _recorder?.IsRecording == true;
    private bool _mapReady = false;
    private bool _hasLocation = false;
    private bool _followMode = true;   // el mapa arranca siguiendo al usuario (followUser=true en JS)
    private bool _headingUp = true;    // modo "Rumbo" (heading-up): activado por defecto
    private static bool _backgroundPromptsChecked = false; // avisos de bateria: una vez por sesion

    private IDispatcherTimer? _recordTimer;

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
            _mapMatch ??= services.GetService<MapMatchService>();

            if (_recorder is null && services.GetService<TrackRecorder>() is { } recorder)
            {
                _recorder = recorder;
                _recorder.PointAdded += OnRecordedPointAdded;
            }

            // La grabacion pudo seguir (o reanudarse sola) con la aplicacion cerrada: la interfaz
            // tiene que reflejar lo que hay, no lo que habia al salir.
            ShowRecordingUi(_isTracking);
            UpdateStateIcon();

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

        // Si una grabacion anterior se quedo a medias, se ofrece recuperarla.
        await OfferPendingRecoveryAsync();

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

            // Listo de verdad, no «a los dos segundos»: se pregunta al WebView si las funciones del
            // mapa ya existen (hasta 15 s). Con un WebView lento, el centrado inicial llegaba
            // antes de que centerOnLocation existiera, se perdia en silencio y el mapa se quedaba
            // en su centro por defecto aunque la cabecera ya enseñara la posicion real.
            for (var i = 0; i < 100 && !_mapReady; i++)
            {
                await Task.Delay(150);
                try
                {
                    var ready = await mapWebView.EvaluateJavaScriptAsync("(typeof centerOnLocation === 'function' && window.mapReady === true) ? 'yes' : 'no'");
                    _mapReady = ready?.Trim('"') == "yes";
                }
                catch (Exception)
                {
                    // El WebView aun no acepta JavaScript: se vuelve a preguntar.
                }
            }
            _mapReady = true;

            // Si la posicion llego antes que el mapa, se centra ahora (era el caso habitual: el
            // GPS con ultima posicion conocida responde en un segundo y el mapa tarda mas).
            if (_lastLocation is not null)
                await CenterMapAsync(_lastLocation, 16);

            // OnAppearing corre antes de que el mapa este listo: se le pasa el estado del Rumbo
            // ahora, que ya puede recibirlo (si no, con Rumbo por defecto el mapa no giraba).
            await mapWebView.EvaluateJavaScriptAsync($"setHeadingUp({(_headingUp ? "true" : "false")});");
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
                _lastLocation = last;
                UpdateLocationDisplay(last);
                await CenterMapWhenReadyAsync(last, 15);
            }

            var current = await _geolocationService.GetCurrentLocationAsync();
            if (current != null)
            {
                _lastLocation = current;
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
        for (int i = 0; i < 100 && !_mapReady; i++)
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

            // Los puntos de la ruta NO se graban aqui: los recibe TrackRecorder desde el servicio
            // en primer plano. Esta pagina solo dibuja, y puede no estar viva mientras se graba.

            // Actualizar mapa con la ubicación actual
            if (_mapReady)
            {
                await UpdateMapLocation(location);
            }

            UpdateRouteFollowing(location);
        });
    }

    /// <summary>
    /// Punto que acaba de grabar el servicio. Puede llegar con la pagina en segundo plano, asi que
    /// se salta el dibujo si el mapa no esta listo: lo que importa (guardarlo) ya esta hecho.
    /// </summary>
    private void OnRecordedPointAdded(object? sender, Location point)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            UpdateRecordingLabels();

            if (_mapReady)
                await AddRoutePointToMap(point);
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

        await mapWebView.EvaluateJavaScriptAsync($"drawRoute([{ToJsCoords(points)}]);");

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

        CancelComparison();

        // El servicio arranca ANTES de marcar la grabacion: es quien escucha al GPS, y asi no se
        // pierde ningun punto entre una cosa y la otra.
        StartTrackingService();
        _recorder?.Start();

        UpdateStateIcon();
        ShowRecordingUi(true);

        // Limpiar mapa
        await ClearMapRoute();
    }

    private async void OnStopTrackingClicked(object sender, EventArgs e)
    {
        if (!_isTracking)
            return;

        _recorder?.Stop();
        UpdateStateIcon();
        ShowRecordingUi(false);
        StopTrackingService();

        // Parar sin ofrecer guardar dejaria la ruta recien grabada colgando hasta la siguiente
        // grabacion, que la borra: es justo cuando hay que preguntar.
        if ((_recorder?.PointCount ?? 0) == 0)
        {
            await ClearMapRoute();
            return;
        }

        var save = await SocShared.ModernDialog.AlertAsync(this,
            L("Ruta grabada"),
            string.Format(L("Se han grabado {0} puntos ({1:0.00} km). ¿Quieres guardarla?"),
                _recorder!.PointCount, _recorder.DistanceKm),
            L("Guardar"), L("Descartar"));

        if (!save)
        {
            OnClearClicked(this, EventArgs.Empty);
            return;
        }

        if (await OfferMapMatchAsync())
            OnSaveRouteClicked(this, EventArgs.Empty);
    }

    /// <summary>
    /// Ofrece ajustar la ruta al mapa antes de guardarla, enseñando las dos versiones en el mapa.
    /// </summary>
    /// <remarks>
    /// <para>Se ENSEÑA, no se hace solo. El ajuste mueve puntos, y mover los datos de alguien sin
    /// que los vea no esta bien ni aunque quede mas bonito: quien ha andado por ahi es quien sabe
    /// si la linea rara era error del GPS o el camino que tomo de verdad. Por eso se pintan la
    /// grabada y la ajustada a la vez (roja la que se guardaria, gris la otra) y se puede
    /// alternar entre ellas antes de decidir; antes se decidia a ciegas con un dialogo de texto.</para>
    ///
    /// <para>Si no hay red o Overpass no responde, se sigue y se guarda la ruta tal cual. Perder
    /// una grabacion por no poder consultar un mapa seria absurdo.</para>
    /// </remarks>
    /// <returns>false si, mientras se comparaba, se borro la ruta o se empezo otra grabacion: ya
    /// no hay nada que guardar.</returns>
    private async Task<bool> OfferMapMatchAsync()
    {
        if (_mapMatch is null || _recorder is null || _recorder.PointCount < 2)
            return true;

        var original = _recorder.Snapshot();

        // Mientras se consulta el mapa se enseña la barra sin botones: la consulta tarda unos
        // segundos y sin esto parecia que la app se habia quedado colgada tras pulsar Guardar.
        compareTitleLabel.Text = L("Ajustando la ruta…");
        compareDetailLabel.Text = L("Consultando los caminos del mapa");
        compareSwapButton.IsVisible = false;
        compareSaveButton.IsVisible = false;
        compareBar.IsVisible = true;

        MatchResult? result;

        try
        {
            result = await _mapMatch.MatchAsync(original);
        }
        catch (Exception)
        {
            result = null;
        }

        if (result is null)
        {
            compareBar.IsVisible = false;
            await SocShared.ModernDialog.AlertAsync(this, L("Ajustar la ruta"),
                L("No se ha podido consultar el mapa. La ruta se guarda tal y como se grabo."), "OK");
            return true;
        }

        if (!result.AnyChange)
        {
            compareBar.IsVisible = false;
            await SocShared.ModernDialog.AlertAsync(this, L("Ajustar la ruta"),
                L("La ruta ya encajaba con el mapa: no ha hecho falta cambiar nada."), "OK");
            return true;
        }

        _compareOriginal = original;
        _compareResult = result;
        _compareShowingAdjusted = true;
        compareSwapButton.IsVisible = true;
        compareSaveButton.IsVisible = true;
        await ShowComparisonAsync();

        _compareChoice = new TaskCompletionSource<bool?>();
        var keepAdjusted = await _compareChoice.Task;
        _compareChoice = null;

        compareBar.IsVisible = false;
        await RunMapJsAsync("clearAltRoute();");

        if (keepAdjusted is null)
            return false;

        if (keepAdjusted.Value)
            _recorder.Adopt(result.Points);

        await RunMapJsAsync($"drawRoute([{ToJsCoords(_recorder.Snapshot())}]);");
        return true;
    }

    /// <summary>Cierra la comparacion sin guardar (se borro la ruta o se empezo a grabar otra).</summary>
    private void CancelComparison() => _compareChoice?.TrySetResult(null);

    // Estado de la comparacion grabada/ajustada (solo vive mientras se ve compareBar).
    private List<Location> _compareOriginal = new();
    private MatchResult? _compareResult;
    private bool _compareShowingAdjusted;
    private TaskCompletionSource<bool?>? _compareChoice;

    /// <summary>Pinta en rojo la version elegida y en gris la otra, y explica cual es cual.</summary>
    private async Task ShowComparisonAsync()
    {
        if (_compareResult is null)
            return;

        var shown = _compareShowingAdjusted ? _compareResult.Points : _compareOriginal;
        var other = _compareShowingAdjusted ? _compareOriginal : _compareResult.Points;

        if (_compareShowingAdjusted)
        {
            compareTitleLabel.Text = L("En rojo: ruta ajustada");
            compareDetailLabel.Text = string.Format(
                L("{0} puntos pegados a caminos y {1} sacados de edificios, de {2}"),
                _compareResult.SnappedToPath, _compareResult.MovedOutOfBuilding, _compareOriginal.Count);
        }
        else
        {
            compareTitleLabel.Text = L("En rojo: ruta grabada");
            compareDetailLabel.Text = string.Format(L("{0} puntos, tal y como se grabó"), _compareOriginal.Count);
        }

        await RunMapJsAsync($"drawAltRoute([{ToJsCoords(other)}]);");
        await RunMapJsAsync($"drawRoute([{ToJsCoords(shown)}]);");
    }

    private async void OnCompareSwapClicked(object sender, EventArgs e)
    {
        _compareShowingAdjusted = !_compareShowingAdjusted;
        await ShowComparisonAsync();
    }

    private void OnCompareSaveClicked(object sender, EventArgs e) =>
        _compareChoice?.TrySetResult(_compareShowingAdjusted);

    private static string ToJsCoords(List<Location> points)
    {
        var ci = System.Globalization.CultureInfo.InvariantCulture;
        return string.Join(",", points.Select(p =>
            $"[{p.Latitude.ToString(ci)},{p.Longitude.ToString(ci)}]"));
    }

    private async Task RunMapJsAsync(string script)
    {
        if (!_mapReady)
            return;

        try
        {
            await mapWebView.EvaluateJavaScriptAsync(script);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error en el mapa: {ex.Message}");
        }
    }

    /// <summary>Traduce si el servicio esta disponible; si no, deja la frase en castellano.</summary>
    private string L(string phrase) =>
        Handler?.MauiContext?.Services.GetService<TranslationService>()?.Translate(phrase) ?? phrase;

    /// <summary>
    /// Cambia entre el boton de grabar y la barra de grabacion, y lleva el contador de tiempo.
    /// </summary>
    private void ShowRecordingUi(bool recording)
    {
        recordButton.IsVisible = !recording;
        recordBar.IsVisible = recording;

        if (recording)
        {
            UpdateRecordingLabels();

            _recordTimer ??= Dispatcher.CreateTimer();
            _recordTimer.Interval = TimeSpan.FromSeconds(1);
            _recordTimer.Tick -= OnRecordTimerTick;
            _recordTimer.Tick += OnRecordTimerTick;
            _recordTimer.Start();
            return;
        }

        _recordTimer?.Stop();
    }

    private void OnRecordTimerTick(object? sender, EventArgs e) => UpdateRecordingLabels();

    private void UpdateRecordingLabels()
    {
        var elapsed = DateTime.Now - (_recorder?.StartedAt ?? DateTime.Now);

        recordTitleLabel.Text = L("Grabando la ruta");
        recordDetailLabel.Text = string.Format(@"{0:hh\:mm\:ss} · {1:0.00} km · {2} {3}",
            elapsed, _recorder?.DistanceKm ?? 0, _recorder?.PointCount ?? 0, L("puntos"));
    }

    /// <summary>
    /// Arranca el servicio en primer plano mientras dure la grabacion. Sin el, Android congela el
    /// proceso al apagar la pantalla y la ruta sale a trozos.
    /// </summary>
    private void StartTrackingService()
    {
#if ANDROID
        TrackingForegroundService.NotificationTitle = "Hiker";
        TrackingForegroundService.NotificationText = L("Grabando la ruta");
        TrackingForegroundService.Start();
#endif
    }

    private void StopTrackingService()
    {
#if ANDROID
        TrackingForegroundService.Stop();
#endif
    }

    private async void OnSaveRouteClicked(object sender, EventArgs e)
    {
        if ((_recorder?.PointCount ?? 0) == 0)
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
                await _routeService.SaveRouteAsync(_recorder!.Snapshot(), routeName);

                // Guardada ya en su GPX, el diario de la grabacion sobra.
                TrackRecorder.DeletePendingJournal();

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
        CancelComparison();
        _recorder?.Discard();
        await ClearMapRoute();
    }

    /// <summary>
    /// Grabacion que se quedo a medias porque Android mato el proceso (pantalla apagada, ruta
    /// larga, poca memoria). Los puntos estan en el diario, asi que se ofrece rescatarlos en vez de
    /// perderlos en silencio.
    /// </summary>
    private async Task OfferPendingRecoveryAsync()
    {
        if (_recorder is null || _recorder.IsRecording || _recorder.PointCount > 0)
            return;

        var pending = TrackRecorder.ReadPendingJournal();
        if (pending.Count < 2)
        {
            // Un punto suelto no es una ruta: se limpia sin molestar a nadie.
            if (pending.Count > 0)
                TrackRecorder.DeletePendingJournal();

            return;
        }

        var recover = await SocShared.ModernDialog.AlertAsync(this,
            L("Grabación interrumpida"),
            string.Format(L("Quedó una grabación sin guardar con {0} puntos. ¿La recuperas?"), pending.Count),
            L("Recuperar"), L("Descartar"));

        if (!recover)
        {
            TrackRecorder.DeletePendingJournal();
            return;
        }

        _recorder.Adopt(pending);
        await DrawRecoveredRouteAsync(pending);
        OnSaveRouteClicked(this, EventArgs.Empty);
    }

    private async Task DrawRecoveredRouteAsync(List<Location> points)
    {
        if (!_mapReady)
            return;

        foreach (var point in points)
            await AddRoutePointToMap(point);
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