using System.Globalization;
using System.Net;
using System.Text;
using Hiker.Presenters;
using Hiker.Services;

namespace Hiker.Tests;

/// <summary>La pantalla del mapa sin pantalla: grabar, guardar, recuperar, ajustar, seguir, Rumbo y avisos.</summary>
public class HomePresenterTests : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    private readonly FakeHomeView _view = new();
    private readonly FakeMap _map = new();
    private readonly FakeDialogs _dialogs = new();
    private readonly SettingsService _settings = new();
    private readonly GpsFilterService _filter;
    private readonly TrackRecorder _recorder;
    private readonly RouteService _routes;
    private int _delays;

    public HomePresenterTests()
    {
        FileSystem.AppDataDirectory = Path.Combine(Path.GetTempPath(), $"hiker-home-{Guid.NewGuid():N}");
        Directory.CreateDirectory(FileSystem.AppDataDirectory);
        Preferences.Values.Clear();
        HomePresenter.ResetSession();
        RouteService.PendingRouteToLoad = null;

        _settings.AppSettings.KalmanFilterEnabled = false;
        _settings.AppSettings.AverageFilterEnabled = false;
        _settings.AppSettings.SpeedFilterEnabled = false;
        _settings.AppSettings.AccuracyFilterEnabled = false;
        _filter = new GpsFilterService(_settings);
        _recorder = new TrackRecorder(_filter);
        _routes = new RouteService(_settings);
    }

    public void Dispose()
    {
        _filter.Dispose();
        RouteService.PendingRouteToLoad = null;
        try { Directory.Delete(FileSystem.AppDataDirectory, true); } catch (IOException) { }
    }

    private HomePresenter Presenter(Func<Task<Stream>>? html = null) =>
        new(_view, _map, _dialogs, new InlineUi(), p => p,
            html ?? (() => Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes("<html>mapa</html>")))),
            _ => { _delays++; return Task.CompletedTask; });

    private static Location P(double lat, double lon, double seconds = 0) =>
        new(lat, lon, 700) { Timestamp = T0.AddSeconds(seconds), Accuracy = 5 };

    private static HttpClient Overpass(Func<HttpResponseMessage> respond) => new(new Handler(respond));

    private sealed class Handler(Func<HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond());
    }

    private static HttpResponseMessage Json(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    /// <summary>Un camino en la latitud 40,0000: lo que pase a menos de 20 m se pega a el.</summary>
    private const string PathAt40 = """
        {"elements":[{"type":"way","geometry":[{"lat":40.0000,"lon":-3.010},{"lat":40.0000,"lon":-2.990}],"tags":{"highway":"path"}}]}
        """;

    private HomeServices Services(ILocationSource? location = null, MapMatchService? match = null,
        ITrackingService? tracking = null, ICompass? compass = null, IBatterySettings? battery = null,
        Microsoft.Maui.Storage.IPreferences? prefs = null, UpdateService? updates = null) => new()
    {
        Location = location,
        Routes = _routes,
        MapMatch = match,
        Recorder = _recorder,
        Tracking = tracking,
        Compass = compass,
        Battery = battery,
        Preferences = prefs,
        Updates = updates,
    };

    private async Task<HomePresenter> ReadyAsync(HomeServices? services = null)
    {
        var presenter = Presenter();
        await presenter.InitializeMapAsync();
        await presenter.OnAppearingAsync(services ?? Services());
        _map.Scripts.Clear();
        _dialogs.Shown.Clear();
        return presenter;
    }

    /// <summary>Graba unos puntos hacia el norte, a unos 11 m del camino de <see cref="PathAt40"/>.</summary>
    private async Task RecordAsync(HomePresenter presenter, int points = 3)
    {
        await presenter.StartTrackingAsync();
        for (var i = 0; i < points; i++)
            _recorder.Push(P(40.0001, -3.0 + i * 0.001, i * 10));
    }

    // -----------------------------------------------------------------------
    // Mapa
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ElMapaSeCargaYSeEsperaAQueEsteListo()
    {
        _map.NotReadyAnswers = 2;
        var presenter = Presenter();

        await presenter.InitializeMapAsync();

        Assert.Equal("<html>mapa</html>", _view.Html);
        Assert.True(presenter.MapReady);
        Assert.Equal(3, _delays);
        Assert.Equal(["setHeadingUp(true);"], _map.Scripts);
    }

    [Fact]
    public async Task SinMapHtmlQuedaUnLienzoVacioYSiElWebViewNoContestaSeSigueTrasQuinceSegundos()
    {
        _map.ThrowsOnReady = true;
        var presenter = Presenter(() => throw new FileNotFoundException("map.html"));

        await presenter.InitializeMapAsync();

        Assert.Equal(HomePresenter.EmptyMapHtml, _view.Html);
        Assert.Equal(100, _delays);
        Assert.True(presenter.MapReady);
    }

    [Fact]
    public async Task UnWebViewRotoNoTumbaLaPantalla()
    {
        _map.Throws = true;
        var presenter = Presenter();

        await presenter.InitializeMapAsync();
        await presenter.SetFollowAsync(false);

        Assert.True(presenter.MapReady);
        Assert.False(presenter.FollowMode);
    }

    [Fact]
    public async Task SiLaPosicionLlegaAntesQueElMapaSeCentraAlEstarListo()
    {
        var location = new FakeLocationSource { LastKnown = P(41, 2) };
        var presenter = Presenter();
        _map.ThrowsOnReady = true;
        await presenter.OnAppearingAsync(Services(location));   // el mapa no esta: espera y no centra
        Assert.DoesNotContain(_map.Scripts, s => s.StartsWith("centerOnLocation"));

        _map.ThrowsOnReady = false;
        await presenter.InitializeMapAsync();

        Assert.Contains("centerOnLocation(41, 2, 16);", _map.Scripts);
    }

    // -----------------------------------------------------------------------
    // Ubicacion
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SinServiciosSoloPoneLosTextosYElIconoDeEspera()
    {
        var presenter = Presenter();

        await presenter.OnAppearingAsync(new HomeServices());

        Assert.Equal(("GPS Tracker", "Obteniendo ubicación...", "ic_st_pause.png", false),
            (_view.Title, _view.Status, _view.Icon, _view.Recording));
    }

    [Fact]
    public async Task AlAparecerArrancaElGpsYCentraConLaUltimaYConUnFixNuevo()
    {
        var location = new FakeLocationSource { LastKnown = P(41, 2), Current = new Location(41.5, 2.5) { Accuracy = 8, Speed = 1 } };
        var presenter = Presenter();
        await presenter.InitializeMapAsync();

        await presenter.OnAppearingAsync(Services(location));
        await presenter.OnAppearingAsync(Services(location));   // volver a la pagina no duplica

        Assert.Equal(2, location.Starts);
        Assert.Equal(1, location.Subscribers);
        Assert.Contains("centerOnLocation(41, 2, 15);", _map.Scripts);
        Assert.Contains("centerOnLocation(41.5, 2.5, 16);", _map.Scripts);
        Assert.Equal(HomePresenter.FormatStatus(location.Current), _view.Status);
        Assert.Equal("ic_st_play.png", _view.Icon);
    }

    [Fact]
    public async Task SiElGpsNoArrancaSeAvisa()
    {
        var presenter = Presenter();
        await presenter.OnAppearingAsync(Services(new FakeLocationSource { StartResult = false }));
        Assert.Contains("Error|No se pudo iniciar el GPS", _dialogs.Shown);

        HomePresenter.ResetSession();
        await Presenter().OnAppearingAsync(Services(new FakeLocationSource { StartThrows = new InvalidOperationException("sin permiso") }));
        Assert.Contains("Error|Error al iniciar el GPS: sin permiso", _dialogs.Shown);
    }

    [Fact]
    public async Task UnFalloAlCentrarAlArrancarNoSeEnseña()
    {
        var presenter = Presenter();
        await presenter.OnAppearingAsync(Services(new FakeLocationSource { CurrentThrows = new TimeoutException() }));
        Assert.Empty(_dialogs.Shown);
    }

    [Fact]
    public async Task CadaPosicionActualizaLaCabeceraYElMapa()
    {
        var location = new FakeLocationSource();
        await ReadyAsync(Services(location));

        location.Raise(new Location(40.5, -3.5) { Accuracy = 12 });
        location.Raise(new Location(40.6, -3.6));

        Assert.Equal(["updateLocation(40.5, -3.5, 12);", "updateLocation(40.6, -3.6, 10);"], _map.Scripts);
        Assert.StartsWith("Lat 40", _view.Status);
    }

    [Fact]
    public async Task ElBotonDeCentrarPideUnFixYSiFallaLoDice()
    {
        var location = new FakeLocationSource { Current = new Location(42, 1) };
        var presenter = await ReadyAsync(Services(location));

        await presenter.CenterOnMeAsync();
        Assert.Equal(["centerOnLocation(42, 1, 16);"], _map.Scripts);

        location.CurrentThrows = new InvalidOperationException("sin GPS");
        await presenter.CenterOnMeAsync();
        Assert.Contains("Error|No se pudo obtener la ubicación: sin GPS", _dialogs.Shown);
    }

    [Fact]
    public async Task AlSalirSeParaElGpsSalvoSiSeEstaGrabando()
    {
        var location = new FakeLocationSource();
        var presenter = await ReadyAsync(Services(location));

        await presenter.OnDisappearingAsync();
        Assert.Equal(1, location.Stops);

        await presenter.StartTrackingAsync();
        await presenter.OnDisappearingAsync();
        Assert.Equal(1, location.Stops);
    }

    // -----------------------------------------------------------------------
    // Grabar y guardar
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GrabarArrancaElServicioYPintaCadaPunto()
    {
        var tracking = new FakeTracking();
        var presenter = await ReadyAsync(Services(tracking: tracking));

        await RecordAsync(presenter, points: 2);
        await presenter.StartTrackingAsync();   // ya grabando: no hace nada

        Assert.Equal(["start Hiker Grabando la ruta"], tracking.Calls);
        Assert.True(_recorder.IsRecording);
        Assert.True(_view.Recording);
        Assert.Equal("ic_st_rec.png", _view.Icon);
        Assert.Equal(["clearRoute();", "addRoutePoint(40.0001, -3);", "addRoutePoint(40.0001, -2.999);"], _map.Scripts);
        Assert.Equal("Grabando la ruta", _view.RecordTitle);
        Assert.EndsWith("2 puntos", _view.RecordDetail);
    }

    [Fact]
    public async Task PararSinPuntosNoPreguntaNada()
    {
        var tracking = new FakeTracking();
        var presenter = await ReadyAsync(Services(tracking: tracking));
        await presenter.StopTrackingAsync();   // sin grabar: nada
        await presenter.StartTrackingAsync();

        await presenter.StopTrackingAsync();

        Assert.Equal(["start Hiker Grabando la ruta", "stop"], tracking.Calls);
        Assert.Empty(_dialogs.Shown);
        Assert.False(_view.Recording);
        Assert.Equal("ic_st_pause.png", _view.Icon);
    }

    [Fact]
    public async Task PararYDescartarBorraLaRuta()
    {
        var presenter = await ReadyAsync();
        await RecordAsync(presenter);
        var openDuring = false;
        _dialogs.DuringConfirm = () => openDuring = presenter.IsKeepOrDiscardOpen;
        _dialogs.Confirms.Enqueue(false);

        await presenter.StopTrackingAsync();

        Assert.True(openDuring);
        Assert.False(presenter.IsKeepOrDiscardOpen);
        Assert.Equal(0, _recorder.PointCount);
        Assert.StartsWith("Ruta grabada|Se han grabado 3 puntos", _dialogs.Shown[0]);
    }

    [Fact]
    public async Task PararYGuardarSinAjusteEscribeElGpxYBorraElDiario()
    {
        var presenter = await ReadyAsync();
        await RecordAsync(presenter);
        _dialogs.Confirms.Enqueue(true);
        _dialogs.Prompts.Enqueue("Paseo");

        await presenter.StopTrackingAsync();

        Assert.True(File.Exists(Path.Combine(RouteService.RoutesDirectory, "Paseo.gpx")));
        Assert.Empty(TrackRecorder.ReadPendingJournal());
        Assert.Equal("Hecho|Ruta «Paseo» guardada.", _dialogs.Shown[^1]);
    }

    [Fact]
    public async Task GuardarSinPuntosSinNombreOSinServicio()
    {
        var presenter = await ReadyAsync();
        await presenter.SaveRouteAsync();
        Assert.Equal(["Aviso|No hay datos de ruta para guardar"], _dialogs.Shown);

        await RecordAsync(presenter);
        _dialogs.Shown.Clear();
        _dialogs.Prompts.Enqueue("  ");
        await presenter.SaveRouteAsync();
        Assert.Single(_dialogs.Shown);   // solo la pregunta del nombre

        var sinRutas = new HomePresenter(_view, _map, _dialogs, new InlineUi(), p => p, () => Task.FromResult<Stream>(new MemoryStream()));
        await sinRutas.OnAppearingAsync(new HomeServices { Recorder = _recorder });
        _dialogs.Prompts.Enqueue("Paseo");
        await sinRutas.SaveRouteAsync();
        Assert.Equal("Error|El servicio de rutas no está disponible.", _dialogs.Shown[^1]);
    }

    [Fact]
    public async Task UnFalloAlGuardarSeEnseña()
    {
        var presenter = await ReadyAsync();
        await RecordAsync(presenter);
        File.WriteAllText(Path.Combine(FileSystem.AppDataDirectory, "routes"), "no es una carpeta");
        _dialogs.Prompts.Enqueue("Paseo");

        await presenter.SaveRouteAsync();

        Assert.StartsWith("Error|Error al guardar la ruta:", _dialogs.Shown[^1]);
    }

    [Fact]
    public async Task LasAccionesDelMenuLlegan()
    {
        var presenter = await ReadyAsync();
        await presenter.RunMapActionAsync("play");
        Assert.True(_recorder.IsRecording);
        _recorder.Push(P(40, -3));

        await presenter.RunMapActionAsync("nada");
        await presenter.RunMapActionAsync("save");   // pregunta el nombre y se cancela
        Assert.Contains("Guardar ruta|Nombre de la ruta:", _dialogs.Shown);

        await presenter.RunMapActionAsync("clear");
        Assert.Equal(0, _recorder.PointCount);

        await presenter.RunMapActionAsync("stop");
        Assert.False(_recorder.IsRecording);
    }

    // -----------------------------------------------------------------------
    // Ajustar al mapa
    // -----------------------------------------------------------------------

    private static async Task Until(Func<bool> condition)
    {
        for (var i = 0; i < 500 && !condition(); i++)
            await Task.Delay(10);
        Assert.True(condition(), "no llego a pasar");
    }

    [Fact]
    public async Task SiNoSePuedeConsultarElMapaSeGuardaTalCual()
    {
        var presenter = await ReadyAsync(Services(match: new MapMatchService(Overpass(() => new HttpResponseMessage(HttpStatusCode.BadGateway)))));
        await RecordAsync(presenter);
        _dialogs.Confirms.Enqueue(true);
        _dialogs.Prompts.Enqueue("Tal cual");

        await presenter.StopTrackingAsync();

        Assert.Contains("Ajustar la ruta|No se ha podido consultar el mapa. La ruta se guarda tal y como se grabó.", _dialogs.Shown);
        Assert.True(File.Exists(Path.Combine(RouteService.RoutesDirectory, "Tal_cual.gpx")));
        Assert.False(_view.CompareVisible);
    }

    [Fact]
    public async Task SiLaRutaYaEncajaNoHayNadaQueComparar()
    {
        var presenter = await ReadyAsync(Services(match: new MapMatchService(Overpass(() => Json("""{"elements":[]}""")))));
        await RecordAsync(presenter);
        _dialogs.Confirms.Enqueue(true);

        await presenter.StopTrackingAsync();

        Assert.Contains("Ajustar la ruta|La ruta ya encajaba con el mapa: no ha hecho falta cambiar nada.", _dialogs.Shown);
    }

    [Fact]
    public async Task SeEnseñanLasDosVersionesYSeGuardaLaElegida()
    {
        var presenter = await ReadyAsync(Services(match: new MapMatchService(Overpass(() => Json(PathAt40)))));
        await RecordAsync(presenter);
        _dialogs.Confirms.Enqueue(true);
        _dialogs.Prompts.Enqueue("Ajustada");

        var stopping = presenter.StopTrackingAsync();
        await Until(() => presenter.IsComparing);

        Assert.True(_view.CompareVisible && _view.CompareButtons);
        Assert.Equal(["Ajustando la ruta…", "En rojo: ruta ajustada"], _view.CompareHistory);
        Assert.Equal("3 puntos pegados a caminos y 0 sacados de edificios, de 3", _view.CompareDetail);

        await presenter.SwapComparisonAsync();
        Assert.Equal("En rojo: ruta grabada", _view.CompareTitle);
        Assert.Equal("3 puntos, tal y como se grabó", _view.CompareDetail);
        await presenter.SwapComparisonAsync();

        presenter.ChooseComparison();
        await stopping;

        Assert.False(_view.CompareVisible);
        Assert.Contains("clearAltRoute();", _map.Scripts);
        Assert.All(_recorder.Snapshot(), p => Assert.Equal(40.0, p.Latitude, 7));   // pegada al camino
        Assert.True(File.Exists(Path.Combine(RouteService.RoutesDirectory, "Ajustada.gpx")));
    }

    [Fact]
    public async Task ElegirLaGrabadaNoMueveLosPuntos()
    {
        var presenter = await ReadyAsync(Services(match: new MapMatchService(Overpass(() => Json(PathAt40)))));
        await RecordAsync(presenter);
        _dialogs.Confirms.Enqueue(true);

        var stopping = presenter.StopTrackingAsync();
        await Until(() => presenter.IsComparing);
        await presenter.SwapComparisonAsync();
        presenter.ChooseComparison();
        await stopping;

        Assert.All(_recorder.Snapshot(), p => Assert.Equal(40.0001, p.Latitude, 7));
    }

    [Fact]
    public async Task BorrarMientrasSeComparaNoGuardaNada()
    {
        var presenter = await ReadyAsync(Services(match: new MapMatchService(Overpass(() => Json(PathAt40)))));
        await RecordAsync(presenter);
        _dialogs.Confirms.Enqueue(true);

        var stopping = presenter.StopTrackingAsync();
        await Until(() => presenter.IsComparing);
        await presenter.ClearAsync();
        await stopping;

        Assert.DoesNotContain(_dialogs.Shown, s => s.StartsWith("Guardar ruta"));
        Assert.Equal(0, _recorder.PointCount);
    }

    // -----------------------------------------------------------------------
    // Grabacion interrumpida
    // -----------------------------------------------------------------------

    /// <summary>Deja en el diario una grabacion a medias, como si Android hubiera matado el proceso.</summary>
    private void Interrupted(int points)
    {
        var other = new TrackRecorder(_filter);
        other.Start();
        for (var i = 0; i < points; i++)
            other.Push(P(40 + i * 0.001, -3, i * 10));
    }

    [Fact]
    public async Task SeOfreceRecuperarUnaGrabacionInterrumpida()
    {
        Interrupted(3);
        _dialogs.Confirms.Enqueue(true);
        _dialogs.Prompts.Enqueue("Recuperada");
        var presenter = Presenter();
        await presenter.InitializeMapAsync();

        await presenter.OnAppearingAsync(Services());

        Assert.StartsWith("Grabación interrumpida|Quedó una grabación sin guardar con 3 puntos", _dialogs.Shown[0]);
        Assert.Equal(3, _map.Scripts.Count(s => s.StartsWith("addRoutePoint")));
        Assert.True(File.Exists(Path.Combine(RouteService.RoutesDirectory, "Recuperada.gpx")));
    }

    [Fact]
    public async Task DescartarLaRecuperacionBorraElDiarioYUnPuntoSueltoSeBorraSinPreguntar()
    {
        Interrupted(2);
        await Presenter().OnAppearingAsync(Services());   // sin respuesta = descartar
        Assert.Empty(TrackRecorder.ReadPendingJournal());

        Interrupted(1);
        _dialogs.Shown.Clear();
        await Presenter().OnAppearingAsync(Services());
        Assert.Empty(TrackRecorder.ReadPendingJournal());
        Assert.Empty(_dialogs.Shown);
    }

    // -----------------------------------------------------------------------
    // Seguir una ruta cargada
    // -----------------------------------------------------------------------

    [Fact]
    public async Task UnaRutaAbiertaDesdeRutasSePintaYSeSigue()
    {
        await _routes.SaveRouteAsync([P(40, -3), P(40.009, -3, 600), P(40.018, -3, 1200)], "Norte");
        RouteService.PendingRouteToLoad = "Norte";
        var location = new FakeLocationSource { Current = new Location(40.009, -3.0001) };
        var presenter = Presenter();
        await presenter.InitializeMapAsync();

        await presenter.OnAppearingAsync(Services(location));

        Assert.Null(RouteService.PendingRouteToLoad);
        Assert.Contains(_map.Scripts, s => s.StartsWith("drawRoute([[40,-3],[40.009,-3]"));
        Assert.True(_view.FollowVisible);
        Assert.StartsWith("En la ruta: a 9 m del trazado", _view.FollowTitle);
        Assert.StartsWith("Quedan 1", _view.FollowDetail);
        Assert.False(_view.OffRoute);

        location.Raise(new Location(40.009, -2.99));
        Assert.StartsWith("Te has salido: a", _view.FollowTitle);
        Assert.StartsWith("Longitud total:", _view.FollowDetail);
        Assert.True(_view.OffRoute);

        presenter.StopFollowing();
        Assert.False(_view.FollowVisible);
        Assert.False(presenter.IsFollowingRoute);
    }

    [Fact]
    public async Task SinMapaListoORutaVaciaNoSePintaNada()
    {
        RouteService.PendingRouteToLoad = "No existe";
        var presenter = Presenter();
        await presenter.InitializeMapAsync();
        await presenter.OnAppearingAsync(Services());
        Assert.Null(RouteService.PendingRouteToLoad);
        Assert.False(_view.FollowVisible);

        File.WriteAllText(Path.Combine(RouteService.RoutesDirectory, "Rota.gpx"), "<gpx");
        RouteService.PendingRouteToLoad = "Rota";
        await presenter.OnAppearingAsync(Services());   // GPX ilegible: no rompe
        Assert.False(_view.FollowVisible);
    }

    // -----------------------------------------------------------------------
    // Seguir y Rumbo
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ElRumboUsaLaBrujulaSoloMientrasSeVeElMapa()
    {
        var compass = new FakeCompass();
        var presenter = await ReadyAsync(Services(compass: compass));
        Assert.True(compass.IsMonitoring);   // Rumbo viene activado
        Assert.Equal(1, compass.Subscribers);

        compass.Raise(90.5);
        Assert.Contains("setHeading(90.5);", _map.Scripts);

        await presenter.OnDisappearingAsync();
        Assert.False(compass.IsMonitoring);
        Assert.Equal(0, compass.Subscribers);

        await presenter.SetHeadingUpAsync(true);
        await presenter.SetHeadingUpAsync(false);
        Assert.False(compass.IsMonitoring);
        Assert.Contains("setHeadingUp(false);", _map.Scripts);

        _map.Scripts.Clear();
        compass.Raise(10);   // ya sin suscripcion
        Assert.Empty(_map.Scripts);
    }

    [Fact]
    public async Task SinBrujulaElMapaSigueEnNorteArriba()
    {
        var rota = new FakeCompass { ThrowOnStart = true };
        var presenter = await ReadyAsync(Services(compass: rota));
        Assert.False(rota.IsMonitoring);

        var sinBrujula = new FakeCompass { IsSupported = false };
        await ReadyAsync(Services(compass: sinBrujula));
        Assert.Equal(0, sinBrujula.Subscribers);

        var noPara = new FakeCompass { IsMonitoring = true, ThrowOnStop = true };
        var otro = await ReadyAsync(Services(compass: noPara));
        await otro.SetHeadingUpAsync(false);   // el fallo al parar no sale
        Assert.False(otro.HeadingUp);
        Assert.False(sinBrujula.IsMonitoring);
    }

    [Fact]
    public async Task LaBrujulaNoMueveElMapaSinRumbo()
    {
        var compass = new FakeCompass();
        var presenter = await ReadyAsync(Services(compass: compass));
        await presenter.SetHeadingUpAsync(false);
        compass.IsMonitoring = true;
        compass.ReadingChanged += (_, _) => { };
        await presenter.SetHeadingUpAsync(true);
        // Rumbo activo de nuevo pero la brujula ya «estaba» en marcha: no se suscribe otra vez.
        _map.Scripts.Clear();
        compass.Raise(45);
        Assert.Empty(_map.Scripts);
    }

    [Fact]
    public async Task SeguirSeLePasaAlMapa()
    {
        var presenter = await ReadyAsync();
        await presenter.SetFollowAsync(false);
        await presenter.SetFollowAsync(true);
        Assert.Equal(["setFollow(false);", "setFollow(true);"], _map.Scripts);
        Assert.True(presenter.FollowMode);
    }

    // -----------------------------------------------------------------------
    // Bateria y version
    // -----------------------------------------------------------------------

    [Fact]
    public async Task LosAvisosDeBateriaSalenUnaVezYAbrenLosAjustes()
    {
        var battery = new FakeBattery();
        var prefs = new FakePreferences();
        _dialogs.Confirms.Enqueue(true);
        _dialogs.Confirms.Enqueue(true);

        var presenter = Presenter();
        await presenter.OnAppearingAsync(Services(battery: battery, prefs: prefs));
        await presenter.OnAppearingAsync(Services(battery: battery, prefs: prefs));   // misma sesion: nada

        Assert.Equal(["optimization", "saver"], battery.Opened);
        Assert.Equal(2, _dialogs.Shown.Count);

        HomePresenter.ResetSession();
        await Presenter().OnAppearingAsync(Services(battery: battery, prefs: prefs));   // ya preguntado
        Assert.Equal(2, _dialogs.Shown.Count);
    }

    [Fact]
    public async Task SinAvisosSiYaEstaExentaOSiFallaLaComprobacion()
    {
        await Presenter().OnAppearingAsync(Services(battery: new FakeBattery { Ignored = true }, prefs: new FakePreferences()));
        HomePresenter.ResetSession();
        await Presenter().OnAppearingAsync(Services(battery: new FakeBattery { Throws = true }, prefs: new FakePreferences()));
        HomePresenter.ResetSession();
        var decline = new FakeBattery();
        await Presenter().OnAppearingAsync(Services(battery: decline, prefs: new FakePreferences()));

        Assert.Empty(decline.Opened);
        Assert.Equal(2, _dialogs.Shown.Count);   // solo los dos de la ultima, contestados que no
    }

    [Fact]
    public async Task AlAparecerSeMiraSiHayVersionNueva()
    {
        var links = new FakeLinks();
        var updates = new UpdateService(new TranslationService(_settings), links,
            () => Task.FromResult("""{"version":"2099.1.1.0","url":"https://example.org/hiker"}"""), () => "2026.10.01.00");
        _dialogs.Confirms.Enqueue(true);

        await Presenter().OnAppearingAsync(Services(updates: updates));

        await Until(() => links.Opened.Count == 1);
        Assert.Equal("https://example.org/hiker", links.Opened[0]);
    }

    // -----------------------------------------------------------------------
    // Formatos
    // -----------------------------------------------------------------------

    [Fact]
    public void FormatosDeLaCabeceraYDelMapa()
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("es-ES");
        try
        {
            Assert.Equal("Lat 40,12346 · Lon -3,00000 · ±8 m · 3,6 km/h",
                HomePresenter.FormatStatus(new Location(40.123456, -3) { Accuracy = 8, Speed = 1 }));
            Assert.Equal("[40.5,-3.25],[41,2]", HomePresenter.ToJsCoords([new Location(40.5, -3.25), new Location(41, 2)]));
            Assert.Equal("01:02:03 · 1,50 km · 7 puntos", HomePresenter.FormatRecording(new TimeSpan(1, 2, 3), 1.5, 7, "puntos"));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }

        Assert.Equal("ic_st_rec.png", HomePresenter.StateIcon(true, false));
        Assert.Equal("ic_st_play.png", HomePresenter.StateIcon(false, true));
        Assert.Equal("ic_st_pause.png", HomePresenter.StateIcon(false, false));
    }

    [Fact]
    public void LosServiciosSeSacanDelProveedor()
    {
        Assert.Null(HomeServices.From(null).Recorder);

        var provider = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton(provider, _recorder);
        var services = HomeServices.From(Microsoft.Extensions.DependencyInjection.ServiceCollectionContainerBuilderExtensions.BuildServiceProvider(provider));

        Assert.Same(_recorder, services.Recorder);
        Assert.Null(services.Location);
    }
}
