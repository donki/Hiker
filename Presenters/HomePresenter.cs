using System.Globalization;
using Hiker.Services;

namespace Hiker.Presenters;

/// <summary>Lo que el presentador del mapa le pide a la pantalla. La implementa HomePage.</summary>
public interface IHomeView
{
    void SetTitle(string text);
    void SetStatus(string text);
    void SetStateIcon(string file);
    void SetMapHtml(string html);
    /// <summary>Boton de grabar o barra de grabacion, con su contador de un segundo.</summary>
    void ShowRecording(bool recording);
    void SetRecordingLabels(string title, string detail);
    void ShowFollowBar(bool visible);
    void SetFollowStatus(string title, string detail, bool offRoute);
    void ShowCompareBar(bool visible, bool withButtons);
    void SetCompareTexts(string title, string detail);
}

/// <summary>El mapa (MapLibre en un WebView): se le habla con JavaScript.</summary>
public interface IMapBridge
{
    Task<string?> EvaluateAsync(string script);
}

/// <summary>Lo que el mapa usa de la app. Todo opcional: la pagina puede aparecer antes que los servicios.</summary>
public sealed class HomeServices
{
    public ILocationSource? Location { get; init; }
    public RouteService? Routes { get; init; }
    public MapMatchService? MapMatch { get; init; }
    public TrackRecorder? Recorder { get; init; }
    public UpdateService? Updates { get; init; }
    public ICompass? Compass { get; init; }
    public ITrackingService? Tracking { get; init; }
    public IBatterySettings? Battery { get; init; }
    public Microsoft.Maui.Storage.IPreferences? Preferences { get; init; }

    public static HomeServices From(IServiceProvider? services) => services is null ? new() : new()
    {
        Location = services.GetService(typeof(ILocationSource)) as ILocationSource,
        Routes = services.GetService(typeof(RouteService)) as RouteService,
        MapMatch = services.GetService(typeof(MapMatchService)) as MapMatchService,
        Recorder = services.GetService(typeof(TrackRecorder)) as TrackRecorder,
        Updates = services.GetService(typeof(UpdateService)) as UpdateService,
        Compass = services.GetService(typeof(ICompass)) as ICompass,
        Tracking = services.GetService(typeof(ITrackingService)) as ITrackingService,
        Battery = services.GetService(typeof(IBatterySettings)) as IBatterySettings,
        Preferences = services.GetService(typeof(Microsoft.Maui.Storage.IPreferences)) as Microsoft.Maui.Storage.IPreferences,
    };
}

/// <summary>
/// La logica de la pantalla del mapa (antes, todo en HomePage.xaml.cs): ubicacion en vivo, grabar,
/// parar y guardar, recuperar una grabacion interrumpida, ajustar la ruta al mapa, seguir una ruta
/// cargada, modo Seguir y modo Rumbo, y los avisos de bateria. La pagina solo pinta.
/// </summary>
public sealed class HomePresenter
{
    /// <summary>HTML vacio por si map.html no se puede leer (va siempre en el paquete).</summary>
    public const string EmptyMapHtml = "<!DOCTYPE html><html><body style='margin:0;background-color:#1a1a1a'></body></html>";

    /// <summary>Avisos de bateria/segundo plano: una vez por sesion.</summary>
    private static bool _backgroundPromptsChecked;

    private readonly IHomeView _view;
    private readonly IMapBridge _map;
    private readonly IUserDialogs _dialogs;
    private readonly IUiThread _ui;
    private readonly Func<string, string> _translate;
    private readonly Func<Task<Stream>> _openMapHtml;
    private readonly Func<int, Task> _delay;
    private readonly RouteFollower _follower;

    private HomeServices _services = new();
    private bool _recorderHooked;
    private bool _hasLocation;
    private Location? _lastLocation;

    // Estado de la comparacion grabada/ajustada (solo vive mientras se ve la barra).
    private List<Location> _compareOriginal = [];
    private MatchResult? _compareResult;
    private bool _compareShowingAdjusted;
    private TaskCompletionSource<bool?>? _compareChoice;

    public HomePresenter(IHomeView view, IMapBridge map, IUserDialogs dialogs, IUiThread ui,
        Func<string, string> translate, Func<Task<Stream>> openMapHtml, Func<int, Task>? delay = null)
    {
        _view = view;
        _map = map;
        _dialogs = dialogs;
        _ui = ui;
        _translate = translate;
        _openMapHtml = openMapHtml;
        _delay = delay ?? (ms => Task.Delay(ms));
        _follower = new RouteFollower(translate);
    }

    public bool MapReady { get; private set; }
    public bool FollowMode { get; private set; } = true;   // el mapa arranca siguiendo al usuario
    public bool HeadingUp { get; private set; } = true;    // modo Rumbo activado por defecto
    public bool IsFollowingRoute => _follower.IsFollowing;
    public bool IsComparing => _compareChoice is not null;

    /// <summary>
    /// Hay a la vista una pregunta cuya respuesta por defecto (cerrar el dialogo) es TIRAR la ruta:
    /// «¿Guardar la ruta grabada?» y «¿Recuperar la grabacion interrumpida?». El boton de atras
    /// cierra los dialogos (Mobile 7), pero estos no: un atras sin querer borraria una caminata.
    /// </summary>
    public bool IsKeepOrDiscardOpen { get; private set; }

    private bool IsTracking => _services.Recorder?.IsRecording == true;

    private string L(string phrase) => _translate(phrase);

    /// <summary>Solo para las pruebas: los avisos de bateria vuelven a salir.</summary>
    internal static void ResetSession() => _backgroundPromptsChecked = false;

    // ==================================================================================
    //  Ciclo de la pagina
    // ==================================================================================

    /// <summary>
    /// Carga el mapa y espera a que este listo de verdad, no «a los dos segundos»: se pregunta al
    /// WebView si las funciones del mapa ya existen (hasta 15 s). Con un WebView lento, el centrado
    /// inicial llegaba antes de que centerOnLocation existiera y se perdia en silencio.
    /// </summary>
    public async Task InitializeMapAsync()
    {
        try
        {
            _view.SetMapHtml(await LoadMapHtmlAsync());

            for (var i = 0; i < 100 && !MapReady; i++)
            {
                await _delay(150);
                try
                {
                    var ready = await _map.EvaluateAsync("(typeof centerOnLocation === 'function' && window.mapReady === true) ? 'yes' : 'no'");
                    MapReady = ready?.Trim('"') == "yes";
                }
                catch (Exception)
                {
                    // El WebView aun no acepta JavaScript: se vuelve a preguntar.
                }
            }
            MapReady = true;

            // Si la posicion llego antes que el mapa, se centra ahora (era el caso habitual).
            if (_lastLocation is not null)
                await CenterMapAsync(_lastLocation, 16);

            // OnAppearing corre antes de que el mapa este listo: se le pasa ahora el estado del Rumbo.
            await _map.EvaluateAsync($"setHeadingUp({JsBool(HeadingUp)});");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error initializing map: {ex.Message}");
        }
    }

    private async Task<string> LoadMapHtmlAsync()
    {
        try
        {
            using var stream = await _openMapHtml();
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync();
        }
        catch (Exception ex)
        {
            // map.html va siempre en el paquete (Resources\Raw, con MapLibre): si aun asi no se puede
            // leer, se deja el lienzo vacio.
            System.Diagnostics.Debug.WriteLine($"Error loading map HTML: {ex.Message}");
            return EmptyMapHtml;
        }
    }

    public async Task OnAppearingAsync(HomeServices services)
    {
        _view.SetTitle(L("GPS Tracker"));
        if (!_hasLocation)
            _view.SetStatus(L("Obteniendo ubicación..."));

        _services = Merge(_services, services);
        if (!_recorderHooked && _services.Recorder is { } recorder)
        {
            recorder.PointAdded += OnRecordedPointAdded;
            _recorderHooked = true;
        }

        // La grabacion pudo seguir (o reanudarse sola) con la aplicacion cerrada: la interfaz
        // tiene que reflejar lo que hay, no lo que habia al salir.
        ShowRecordingUi(IsTracking);
        UpdateStateIcon();

        if (_services.Location is { } location)
        {
            location.LocationChanged -= OnLocationChanged;
            location.LocationChanged += OnLocationChanged;
            await StartLocationUpdatesAsync(location);
            await CenterOnInitialLocationAsync(location);
        }

        // Si venimos de la pantalla de Rutas con una ruta seleccionada, se pinta en el mapa.
        await LoadPendingRouteAsync();

        // Si una grabacion anterior se quedo a medias, se ofrece recuperarla.
        await OfferPendingRecoveryAsync();

        // Si el modo Rumbo estaba activo, reanuda la brujula al volver a la pagina.
        if (HeadingUp)
            await SetHeadingUpAsync(true);

        await CheckBackgroundPermissionsAsync();

        // Comprobacion de version al arrancar (constitucion seccion 15). No bloqueante.
        if (_services.Updates is { } updates)
            _ = updates.CheckAndPromptAsync(_dialogs);
    }

    /// <summary>Lo que ya se tenia no se pierde si el proveedor devuelve menos en otra vuelta.</summary>
    private static HomeServices Merge(HomeServices old, HomeServices now) => new()
    {
        Location = old.Location ?? now.Location,
        Routes = old.Routes ?? now.Routes,
        MapMatch = old.MapMatch ?? now.MapMatch,
        Recorder = old.Recorder ?? now.Recorder,
        Updates = now.Updates ?? old.Updates,
        Compass = old.Compass ?? now.Compass,
        Tracking = old.Tracking ?? now.Tracking,
        Battery = old.Battery ?? now.Battery,
        Preferences = old.Preferences ?? now.Preferences,
    };

    public async Task OnDisappearingAsync()
    {
        // La brujula solo hace falta mientras se ve el mapa.
        StopCompass();

        // Si se esta grabando, NO se para el GPS al cambiar de pestaña: se perderia la traza.
        if (IsTracking)
            return;

        if (_services.Location is { } location)
            await location.ListeningStopAsync();
    }

    // ==================================================================================
    //  Ubicacion
    // ==================================================================================

    private async Task StartLocationUpdatesAsync(ILocationSource location)
    {
        try
        {
            if (!await location.ListeningStartAsync())
                await _dialogs.AlertAsync(L("Error"), L("No se pudo iniciar el GPS"), "OK");
        }
        catch (Exception ex)
        {
            await _dialogs.AlertAsync(L("Error"), string.Format(L("Error al iniciar el GPS: {0}"), ex.Message), "OK");
        }
    }

    /// <summary>
    /// Centra el mapa cuanto antes: primero con la ultima ubicacion conocida (instantanea) y luego
    /// con un fix fresco. La etiqueta de ubicacion se pone en cuanto hay fix, sin esperar al mapa.
    /// </summary>
    private async Task CenterOnInitialLocationAsync(ILocationSource location)
    {
        try
        {
            if (await location.GetLastKnownLocationAsync() is { } last)
            {
                UpdateLocationDisplay(last);
                await CenterMapWhenReadyAsync(last, 15);
            }

            if (await location.GetCurrentLocationAsync() is { } current)
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

    private Task CenterMapAsync(Location location, int zoom) =>
        _map.EvaluateAsync($"centerOnLocation({Num(location.Latitude)}, {Num(location.Longitude)}, {zoom});");

    /// <summary>Centra el mapa cuando el WebView este listo (espera hasta ~15 s).</summary>
    private async Task CenterMapWhenReadyAsync(Location location, int zoom)
    {
        for (var i = 0; i < 100 && !MapReady; i++)
            await _delay(150);
        if (MapReady)
            await CenterMapAsync(location, zoom);
    }

    /// <summary>Posicion nueva del GPS (llega de otro hilo).</summary>
    public void OnLocationChanged(Location location) => _ui.Post(async () =>
    {
        UpdateLocationDisplay(location);

        // Los puntos de la ruta NO se graban aqui: los recibe TrackRecorder desde el servicio en
        // primer plano. Esta pagina solo dibuja, y puede no estar viva mientras se graba.
        if (MapReady)
            await RunMapJsAsync($"updateLocation({Num(location.Latitude)}, {Num(location.Longitude)}, {(location.Accuracy is { } a ? Num(a) : "10")});");

        UpdateRouteFollowing(location);
    });

    /// <summary>Punto que acaba de grabar el servicio. Puede llegar con la pagina en segundo plano.</summary>
    private void OnRecordedPointAdded(object? sender, Location point) => _ui.Post(async () =>
    {
        UpdateRecordingLabels();
        await AddRoutePointToMapAsync(point);
    });

    /// <summary>Todo en UNA linea: Lat · Lon · precision · velocidad.</summary>
    public static string FormatStatus(Location location)
    {
        var speedKmh = (location.Speed ?? 0) * 3.6; // m/s -> km/h
        return $"Lat {location.Latitude:F5} · Lon {location.Longitude:F5} · ±{location.Accuracy:F0} m · {speedKmh:F1} km/h";
    }

    private void UpdateLocationDisplay(Location location)
    {
        _view.SetStatus(FormatStatus(location));
        _hasLocation = true;
        _lastLocation = location;   // la usa el seguimiento al cargar una ruta
        UpdateStateIcon();
    }

    /// <summary>Icono de estado: grabando (rojo), localizado (play) o en espera (pausa).</summary>
    public static string StateIcon(bool tracking, bool hasLocation) =>
        tracking ? "ic_st_rec.png" : hasLocation ? "ic_st_play.png" : "ic_st_pause.png";

    private void UpdateStateIcon() => _view.SetStateIcon(StateIcon(IsTracking, _hasLocation));

    /// <summary>Boton de centrar: pide un fix y centra el mapa en el.</summary>
    public async Task CenterOnMeAsync()
    {
        try
        {
            if (_services.Location is { } location
                && await location.GetCurrentLocationAsync() is { } current && MapReady)
                await CenterMapAsync(current, 16);
        }
        catch (Exception ex)
        {
            await _dialogs.AlertAsync(L("Error"), string.Format(L("No se pudo obtener la ubicación: {0}"), ex.Message), "OK");
        }
    }

    // ==================================================================================
    //  Mapa
    // ==================================================================================

    private Task AddRoutePointToMapAsync(Location location) =>
        RunMapJsAsync($"addRoutePoint({Num(location.Latitude)}, {Num(location.Longitude)});");

    private async Task ClearMapRouteAsync()
    {
        StopFollowing();   // si se borra el trazado, no queda ruta que seguir
        await RunMapJsAsync("clearRoute();");
    }

    /// <summary>Ejecuta JavaScript en el mapa si esta listo; un fallo del WebView no tumba la pantalla.</summary>
    private async Task RunMapJsAsync(string script)
    {
        if (!MapReady)
            return;

        try
        {
            await _map.EvaluateAsync(script);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error en el mapa: {ex.Message}");
        }
    }

    public static string ToJsCoords(IEnumerable<Location> points) =>
        string.Join(",", points.Select(p => $"[{Num(p.Latitude)},{Num(p.Longitude)}]"));

    private static string Num(double value) => value.ToString(CultureInfo.InvariantCulture);

    private static string JsBool(bool value) => value ? "true" : "false";

    // ==================================================================================
    //  Seguir una ruta cargada
    // ==================================================================================

    /// <summary>Si la pantalla de Rutas pidió abrir una ruta, la carga y la pinta.</summary>
    private async Task LoadPendingRouteAsync()
    {
        var name = RouteService.PendingRouteToLoad;
        if (string.IsNullOrEmpty(name) || _services.Routes is null)
            return;

        RouteService.PendingRouteToLoad = null;
        try
        {
            var points = await _services.Routes.LoadRouteLocationsAsync(name);
            if (!MapReady || points.Count == 0)
                return;

            await _map.EvaluateAsync($"drawRoute([{ToJsCoords(points)}]);");
            StartFollowing(points);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading pending route: {ex.Message}");
        }
    }

    private void StartFollowing(List<Location> points)
    {
        var status = _follower.Start(points);
        _view.ShowFollowBar(true);
        _view.SetFollowStatus(status.Title, status.Detail, status.OffRoute);

        // Si ya hay posicion, no esperar al siguiente punto GPS para decir si estas en la ruta.
        if (_lastLocation is not null)
            UpdateRouteFollowing(_lastLocation);
    }

    public void StopFollowing()
    {
        _follower.Stop();
        _view.ShowFollowBar(false);
    }

    private void UpdateRouteFollowing(Location current)
    {
        if (_follower.Update(current) is { } status)
            _view.SetFollowStatus(status.Title, status.Detail, status.OffRoute);
    }

    // ==================================================================================
    //  Grabar, parar, guardar, borrar
    // ==================================================================================

    /// <summary>Accion del submenu del mapa (play=grabar, stop=parar, save=guardar, clear=borrar).</summary>
    public Task RunMapActionAsync(string action) => action switch
    {
        "play" => StartTrackingAsync(),
        "stop" => StopTrackingAsync(),
        "save" => SaveRouteAsync(),
        "clear" => ClearAsync(),
        _ => Task.CompletedTask,
    };

    public async Task StartTrackingAsync()
    {
        if (IsTracking)
            return;

        CancelComparison();

        // El servicio arranca ANTES de marcar la grabacion: es quien escucha al GPS, y asi no se
        // pierde ningun punto entre una cosa y la otra.
        _services.Tracking?.Start("Hiker", L("Grabando la ruta"));
        _services.Recorder?.Start();

        UpdateStateIcon();
        ShowRecordingUi(true);
        await ClearMapRouteAsync();
    }

    public async Task StopTrackingAsync()
    {
        if (!IsTracking)
            return;

        var recorder = _services.Recorder!;
        recorder.Stop();
        UpdateStateIcon();
        ShowRecordingUi(false);
        _services.Tracking?.Stop();

        // Parar sin ofrecer guardar dejaria la ruta recien grabada colgando hasta la siguiente
        // grabacion, que la borra: es justo cuando hay que preguntar.
        if (recorder.PointCount == 0)
        {
            await ClearMapRouteAsync();
            return;
        }

        IsKeepOrDiscardOpen = true;
        var save = await _dialogs.ConfirmAsync(
            L("Ruta grabada"),
            string.Format(L("Se han grabado {0} puntos ({1:0.00} km). ¿Quieres guardarla?"), recorder.PointCount, recorder.DistanceKm),
            L("Guardar"), L("Descartar"));
        IsKeepOrDiscardOpen = false;

        if (!save)
        {
            await ClearAsync();
            return;
        }

        if (await OfferMapMatchAsync())
            await SaveRouteAsync();
    }

    public async Task SaveRouteAsync()
    {
        var recorder = _services.Recorder;
        if (recorder is null || recorder.PointCount == 0)
        {
            await _dialogs.AlertAsync(L("Aviso"), L("No hay datos de ruta para guardar"), "OK");
            return;
        }

        try
        {
            var routeName = await _dialogs.PromptAsync(L("Guardar ruta"), L("Nombre de la ruta:"), L("Guardar"), L("Cancelar"));
            if (string.IsNullOrWhiteSpace(routeName))
                return;

            if (_services.Routes is null)
            {
                await _dialogs.AlertAsync(L("Error"), L("El servicio de rutas no está disponible."), "OK");
                return;
            }

            // Guardado real: escribe la ruta como GPX en el almacenamiento de la app; guardada ya,
            // el diario de la grabacion sobra.
            await _services.Routes.SaveRouteAsync(recorder.Snapshot(), routeName);
            TrackRecorder.DeletePendingJournal();

            await _dialogs.AlertAsync(L("Hecho"), string.Format(L("Ruta «{0}» guardada."), routeName), "OK");
        }
        catch (Exception ex)
        {
            await _dialogs.AlertAsync(L("Error"), string.Format(L("Error al guardar la ruta: {0}"), ex.Message), "OK");
        }
    }

    public async Task ClearAsync()
    {
        CancelComparison();
        _services.Recorder?.Discard();
        await ClearMapRouteAsync();
    }

    /// <summary>Cambia entre el boton de grabar y la barra de grabacion.</summary>
    private void ShowRecordingUi(bool recording)
    {
        if (recording)
            UpdateRecordingLabels();
        _view.ShowRecording(recording);
    }

    /// <summary>Tiempo, kilometros y puntos de la grabacion (la pagina lo llama cada segundo).</summary>
    public void UpdateRecordingLabels()
    {
        var recorder = _services.Recorder;
        var elapsed = recorder is null ? TimeSpan.Zero : DateTime.Now - recorder.StartedAt;
        _view.SetRecordingLabels(L("Grabando la ruta"),
            FormatRecording(elapsed, recorder?.DistanceKm ?? 0, recorder?.PointCount ?? 0, L("puntos")));
    }

    public static string FormatRecording(TimeSpan elapsed, double km, int points, string pointsWord) =>
        string.Format(@"{0:hh\:mm\:ss} · {1:0.00} km · {2} {3}", elapsed, km, points, pointsWord);

    // ==================================================================================
    //  Ajustar la ruta al mapa
    // ==================================================================================

    /// <summary>
    /// Ofrece ajustar la ruta al mapa antes de guardarla, enseñando las dos versiones en el mapa.
    /// Se ENSEÑA, no se hace solo: quien ha andado por ahi es quien sabe si la linea rara era error
    /// del GPS o el camino que tomo de verdad. Si no hay red o Overpass no responde, se guarda la
    /// ruta tal cual.
    /// </summary>
    /// <returns>false si, mientras se comparaba, se borro la ruta o se empezo otra grabacion.</returns>
    private async Task<bool> OfferMapMatchAsync()
    {
        var recorder = _services.Recorder!;
        if (_services.MapMatch is null || recorder.PointCount < 2)
            return true;

        var original = recorder.Snapshot();

        // Mientras se consulta el mapa se enseña la barra sin botones: la consulta tarda unos
        // segundos y sin esto parecia que la app se habia quedado colgada tras pulsar Guardar.
        _view.SetCompareTexts(L("Ajustando la ruta…"), L("Consultando los caminos del mapa"));
        _view.ShowCompareBar(true, withButtons: false);

        MatchResult? result;
        try
        {
            result = await _services.MapMatch.MatchAsync(original);
        }
        catch (Exception)
        {
            result = null;
        }

        if (result is null || !result.AnyChange)
        {
            _view.ShowCompareBar(false, withButtons: false);
            await _dialogs.AlertAsync(L("Ajustar la ruta"), result is null
                ? L("No se ha podido consultar el mapa. La ruta se guarda tal y como se grabó.")
                : L("La ruta ya encajaba con el mapa: no ha hecho falta cambiar nada."), "OK");
            return true;
        }

        _compareOriginal = original;
        _compareResult = result;
        _compareShowingAdjusted = true;
        _view.ShowCompareBar(true, withButtons: true);
        await ShowComparisonAsync();

        _compareChoice = new TaskCompletionSource<bool?>();
        var keepAdjusted = await _compareChoice.Task;
        _compareChoice = null;

        _view.ShowCompareBar(false, withButtons: false);
        await RunMapJsAsync("clearAltRoute();");

        if (keepAdjusted is null)
            return false;

        if (keepAdjusted.Value)
            recorder.Adopt(result.Points);

        await RunMapJsAsync($"drawRoute([{ToJsCoords(recorder.Snapshot())}]);");
        return true;
    }

    /// <summary>Cierra la comparacion sin guardar (se borro la ruta o se empezo a grabar otra).</summary>
    private void CancelComparison() => _compareChoice?.TrySetResult(null);

    /// <summary>Pinta en rojo la version elegida y en gris la otra, y explica cual es cual.</summary>
    private async Task ShowComparisonAsync()
    {
        if (_compareResult is null)
            return;

        var shown = _compareShowingAdjusted ? _compareResult.Points : _compareOriginal;
        var other = _compareShowingAdjusted ? _compareOriginal : _compareResult.Points;

        if (_compareShowingAdjusted)
            _view.SetCompareTexts(L("En rojo: ruta ajustada"), string.Format(
                L("{0} puntos pegados a caminos y {1} sacados de edificios, de {2}"),
                _compareResult.SnappedToPath, _compareResult.MovedOutOfBuilding, _compareOriginal.Count));
        else
            _view.SetCompareTexts(L("En rojo: ruta grabada"),
                string.Format(L("{0} puntos, tal y como se grabó"), _compareOriginal.Count));

        await RunMapJsAsync($"drawAltRoute([{ToJsCoords(other)}]);");
        await RunMapJsAsync($"drawRoute([{ToJsCoords(shown)}]);");
    }

    public async Task SwapComparisonAsync()
    {
        _compareShowingAdjusted = !_compareShowingAdjusted;
        await ShowComparisonAsync();
    }

    public void ChooseComparison() => _compareChoice?.TrySetResult(_compareShowingAdjusted);

    // ==================================================================================
    //  Grabacion interrumpida
    // ==================================================================================

    /// <summary>
    /// Grabacion que se quedo a medias porque Android mato el proceso. Los puntos estan en el
    /// diario, asi que se ofrece rescatarlos en vez de perderlos en silencio.
    /// </summary>
    private async Task OfferPendingRecoveryAsync()
    {
        var recorder = _services.Recorder;
        if (recorder is null || recorder.IsRecording || recorder.PointCount > 0)
            return;

        var pending = TrackRecorder.ReadPendingJournal();
        if (pending.Count < 2)
        {
            // Un punto suelto no es una ruta: se limpia sin molestar a nadie.
            if (pending.Count > 0)
                TrackRecorder.DeletePendingJournal();
            return;
        }

        IsKeepOrDiscardOpen = true;
        var recover = await _dialogs.ConfirmAsync(
            L("Grabación interrumpida"),
            string.Format(L("Quedó una grabación sin guardar con {0} puntos. ¿La recuperas?"), pending.Count),
            L("Recuperar"), L("Descartar"));
        IsKeepOrDiscardOpen = false;

        if (!recover)
        {
            TrackRecorder.DeletePendingJournal();
            return;
        }

        recorder.Adopt(pending);
        foreach (var point in pending)
            await AddRoutePointToMapAsync(point);
        await SaveRouteAsync();
    }

    // ==================================================================================
    //  Seguir y Rumbo
    // ==================================================================================

    /// <summary>Activa/desactiva el modo "Seguir": recentrar el mapa en cada punto GPS.</summary>
    public async Task SetFollowAsync(bool enabled)
    {
        FollowMode = enabled;
        await RunMapJsAsync($"setFollow({JsBool(enabled)});");
    }

    /// <summary>Modo "Rumbo" (heading-up): rota el mapa segun la brujula del dispositivo.</summary>
    public async Task SetHeadingUpAsync(bool enabled)
    {
        HeadingUp = enabled;
        await RunMapJsAsync($"setHeadingUp({JsBool(enabled)});");

        if (!enabled)
        {
            StopCompass();
            return;
        }

        try
        {
            if (_services.Compass is { IsSupported: true, IsMonitoring: false } compass)
            {
                compass.ReadingChanged += OnCompassReadingChanged;
                compass.Start(SensorSpeed.UI);
            }
        }
        catch (Exception ex)
        {
            // Sin brujula (FeatureNotSupportedException u otro fallo): el mapa sigue en norte-arriba.
            System.Diagnostics.Debug.WriteLine($"Error en modo Rumbo: {ex.Message}");
        }
    }

    private void StopCompass()
    {
        try
        {
            if (_services.Compass is { IsSupported: true, IsMonitoring: true } compass)
            {
                compass.Stop();
                compass.ReadingChanged -= OnCompassReadingChanged;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error deteniendo brújula: {ex.Message}");
        }
    }

    private void OnCompassReadingChanged(object? sender, CompassChangedEventArgs e)
    {
        if (!HeadingUp || !MapReady)
            return;

        var heading = e.Reading.HeadingMagneticNorth;
        _ui.Post(() => RunMapJsAsync($"setHeading({Num(heading)});"));
    }

    // ==================================================================================
    //  Bateria y segundo plano
    // ==================================================================================

    /// <summary>
    /// Avisos de optimización de batería y ejecución en segundo plano, con ModernDialog (la
    /// constitución prohíbe los AlertDialog nativos). Una vez cada uno (Preferences), solo en Android.
    /// </summary>
    private async Task CheckBackgroundPermissionsAsync()
    {
        if (_backgroundPromptsChecked)
            return;
        _backgroundPromptsChecked = true;

        var battery = _services.Battery;
        var prefs = _services.Preferences;
        if (battery is null || prefs is null)
            return;

        try
        {
            if (battery.IsOptimizationIgnored())
                return; // ya esta exenta: no hace falta preguntar nada

            if (!prefs.Get("prompt_battery_opt", false))
            {
                prefs.Set("prompt_battery_opt", true);
                if (await _dialogs.ConfirmAsync(L("Optimización de batería"),
                        L("Para mejorar el posicionamiento y el rendimiento de Hiker, permite que la aplicación funcione sin restricciones de batería. ¿Deseas modificar esta configuración ahora?"),
                        L("Sí"), L("No")))
                    battery.OpenOptimizationSettings();
            }

            if (!prefs.Get("prompt_background_exec", false))
            {
                prefs.Set("prompt_background_exec", true);
                if (await _dialogs.ConfirmAsync(L("Ejecución en segundo plano"),
                        L("Para que Hiker funcione correctamente en segundo plano, permite la ejecución sin restricciones. ¿Deseas modificar esta configuración ahora?"),
                        L("Sí"), L("No")))
                    battery.OpenBatterySaverSettings();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error comprobando permisos de segundo plano: {ex.Message}");
        }
    }
}
