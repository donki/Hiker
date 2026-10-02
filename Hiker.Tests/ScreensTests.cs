using System.ComponentModel;
using System.Globalization;
using Hiker.Presenters;
using Hiker.Services;

namespace Hiker.Tests;

/// <summary>Rutas, ficha de una ruta (con su perfil) y menu lateral, sin pantallas.</summary>
public class ScreensTests : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 27, 10, 0, 0, TimeSpan.Zero);

    private readonly RouteService _routes = new(new SettingsService());
    private readonly FakeDialogs _dialogs = new();
    private readonly FakeNavigator _navigator = new();
    private readonly FakePicker _picker = new();
    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;

    public ScreensTests()
    {
        FileSystem.AppDataDirectory = Path.Combine(Path.GetTempPath(), $"hiker-screens-{Guid.NewGuid():N}");
        Directory.CreateDirectory(FileSystem.AppDataDirectory);
        RouteService.PendingRouteToLoad = null;
        CultureInfo.CurrentCulture = new CultureInfo("es-ES");
    }

    public void Dispose()
    {
        CultureInfo.CurrentCulture = _culture;
        RouteService.PendingRouteToLoad = null;
        try { Directory.Delete(FileSystem.AppDataDirectory, true); } catch (IOException) { }
    }

    private static Location P(double lat, double minutes, double alt) =>
        new(lat, -3, alt) { Timestamp = T0.AddMinutes(minutes) };

    private RoutesPresenter Routes(RouteService? service = null, bool withService = true) =>
        new(() => withService ? service ?? _routes : null, p => p, _dialogs, _navigator, _picker);

    // -----------------------------------------------------------------------
    // Rutas
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ListaLasRutasConSusRotulos()
    {
        await _routes.SaveRouteAsync([P(40, 0, 600), P(40.009, 15, 900)], "Subida");
        var presenter = Routes();

        await presenter.LoadAsync();
        await presenter.LoadAsync();   // recargar no duplica

        var route = Assert.Single(presenter.Routes);
        Assert.Equal("Subida", route.Name);
        Assert.Equal("Distancia: 1,00 km", route.DistanceText);
        Assert.StartsWith("Fecha: ", route.DateText);
        Assert.NotNull(route.Route);
        Assert.Equal(("Rutas Guardadas", "Gestión de Rutas", "Cargar GPX", "Actualizar"), presenter.Texts);
    }

    [Fact]
    public async Task SinServicioLaListaQuedaVaciaYUnFalloSeAvisa()
    {
        var sin = Routes(withService: false);
        await sin.LoadAsync();
        Assert.Empty(sin.Routes);

        var roto = new RoutesPresenter(() => throw new InvalidOperationException("roto"), p => p, _dialogs, _navigator, _picker);
        await roto.LoadAsync();
        Assert.Equal("Error|Error cargando las rutas: roto", _dialogs.Shown[0]);
    }

    [Fact]
    public async Task AbrirEnElMapaYVerLaFicha()
    {
        var presenter = Routes();
        var route = new RouteInfo { Name = "Subida" };

        await presenter.OpenOnMapAsync(route);
        await presenter.ShowInfoAsync(route);
        await Routes(withService: false).ShowInfoAsync(route);   // sin servicio: nada

        Assert.Equal("Subida", RouteService.PendingRouteToLoad);
        Assert.Equal(["//HomePage", "info:Subida"], _navigator.Routes);

        _navigator.Throws = true;
        await presenter.OpenOnMapAsync(route);
        Assert.Equal("Error|Error cargando la ruta: sin Shell", _dialogs.Shown[0]);
    }

    [Fact]
    public async Task BorrarPideConfirmacion()
    {
        await _routes.SaveRouteAsync([P(40, 0, 600)], "Borrable");
        var presenter = Routes();
        await presenter.LoadAsync();
        var route = presenter.Routes[0];

        await presenter.DeleteAsync(route);   // dice que no
        Assert.Single(presenter.Routes);

        _dialogs.Confirms.Enqueue(true);
        await presenter.DeleteAsync(route);
        Assert.Empty(presenter.Routes);
        Assert.Empty(await _routes.GetAllRoutesAsync());
        Assert.Equal("Confirmar|¿Eliminar la ruta «Borrable»?", _dialogs.Shown[0]);
    }

    [Fact]
    public async Task UnFalloAlBorrarSeAvisa()
    {
        var presenter = new RoutesPresenter(() => _routes, p => p, _dialogs, _navigator, _picker);
        Directory.CreateDirectory(Path.Combine(FileSystem.AppDataDirectory, "routes"));
        File.WriteAllText(Path.Combine(FileSystem.AppDataDirectory, "routes", "Abierta.gpx"), "x");
        using var locked = File.Open(Path.Combine(FileSystem.AppDataDirectory, "routes", "Abierta.gpx"), FileMode.Open, FileAccess.Read, FileShare.None);
        _dialogs.Confirms.Enqueue(true);

        await presenter.DeleteAsync(new RouteInfo { Name = "Abierta" });

        Assert.StartsWith("Error|Error eliminando la ruta:", _dialogs.Shown[^1]);
    }

    [Fact]
    public async Task ImportarUnGpx()
    {
        _picker.File = ("Mi ruta.gpx", """<gpx><trk><trkseg><trkpt lat="40" lon="-3"><ele>600</ele></trkpt><trkpt lat="40.01" lon="-3"/></trkseg></trk></gpx>""");
        var presenter = Routes();

        await presenter.ImportGpxAsync();

        Assert.Equal("Mi_ruta", Assert.Single(presenter.Routes).Name);   // el nombre del fichero se normaliza
        Assert.Equal("Hecho|Ruta «Mi ruta» importada.", _dialogs.Shown[^1]);
    }

    [Fact]
    public async Task ImportarSinServicioCanceladoVacioORoto()
    {
        await Routes(withService: false).ImportGpxAsync();
        Assert.Equal("Error|No se pudo acceder al servicio de rutas. Cierra y vuelve a abrir la aplicación.", _dialogs.Shown[^1]);

        _dialogs.Shown.Clear();
        await Routes().ImportGpxAsync();   // cancelado
        Assert.Empty(_dialogs.Shown);

        _picker.File = ("vacio.gpx", "<gpx/>");
        await Routes().ImportGpxAsync();
        Assert.Equal("Aviso|El archivo GPX no contiene puntos.", _dialogs.Shown[^1]);

        _picker.File = ("roto.gpx", "<gpx");
        await Routes().ImportGpxAsync();
        Assert.StartsWith("Error|Error cargando el GPX:", _dialogs.Shown[^1]);
    }

    // -----------------------------------------------------------------------
    // Ficha de una ruta
    // -----------------------------------------------------------------------

    [Fact]
    public async Task LaFichaCalculaYAvisaDeLosCambios()
    {
        // ~1 km en 15 min subiendo 300 m y ~1 km en 25 min bajando 200 m.
        await _routes.SaveRouteAsync([P(40, 0, 600), P(40.009, 15, 900), P(40.018, 40, 700)], "Subida");
        var presenter = new RouteInfoPresenter("Subida", _routes, p => p, _dialogs, _navigator);
        var changed = new List<string?>();
        ((INotifyPropertyChanged)presenter).PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        Assert.True(await presenter.LoadAsync(dark: true));

        Assert.Equal([string.Empty], changed);
        Assert.Equal(("2,00 km", "+300 m", "−200 m"), (presenter.DistanceValue, presenter.GainValue, presenter.LossValue));
        Assert.Equal(("900 m", "600 m", "300 m"), (presenter.MaxAltValue, presenter.MinAltValue, presenter.RangeValue));
        Assert.Equal(("40:00", "40:00", "19:59 /km"), (presenter.DurationValue, presenter.MovingValue, presenter.PaceValue));
        Assert.Equal(("3,0 km/h", "4,0 km/h", "3"), (presenter.AvgSpeedValue, presenter.MaxSpeedValue, presenter.PointsValue));
        Assert.Equal(T0.ToLocalTime().ToString("f", CultureInfo.CurrentCulture), presenter.Date);
        Assert.True(presenter.Profile.Dark);
        Assert.Equal(3, presenter.Profile.Profile.Count);

        await presenter.ShowOnMapAsync();
        Assert.Equal("Subida", RouteService.PendingRouteToLoad);
        Assert.Equal(["//HomePage"], _navigator.Routes);
    }

    [Fact]
    public async Task UnaFichaSinAltitudNiHorasLlevaGuiones()
    {
        var presenter = new RouteInfoPresenter("No existe", _routes, p => p, _dialogs, _navigator);

        Assert.True(await presenter.LoadAsync(dark: false));

        Assert.Equal(("—", "—", "—", "—", "—", "—", "—"), (presenter.MaxAltValue, presenter.MinAltValue, presenter.RangeValue,
            presenter.DurationValue, presenter.MovingValue, presenter.PaceValue, presenter.AvgSpeedValue));
        Assert.Equal(("—", "0", "Esta ruta no lleva altitud."), (presenter.MaxSpeedValue, presenter.PointsValue, presenter.Profile.NoData));
        Assert.Equal("No existe", presenter.Name);
    }

    [Fact]
    public async Task UnaFichaIlegibleSeAvisa()
    {
        File.WriteAllText(Path.Combine(RouteService.RoutesDirectory, "Rota.gpx"), "<gpx");
        var presenter = new RouteInfoPresenter("Rota", _routes, p => p, _dialogs, _navigator);

        Assert.False(await presenter.LoadAsync(dark: false));
        Assert.Single(_dialogs.Shown);
    }

    [Fact]
    public void RotulosDeLaFicha()
    {
        var presenter = new RouteInfoPresenter("x", _routes, p => "[" + p + "]", _dialogs, _navigator);
        string[] titles =
        [
            presenter.DistanceTitle, presenter.GainTitle, presenter.LossTitle, presenter.MaxAltTitle, presenter.MinAltTitle,
            presenter.RangeTitle, presenter.DurationTitle, presenter.MovingTitle, presenter.PaceTitle, presenter.AvgSpeedTitle,
            presenter.MaxSpeedTitle, presenter.PointsTitle, presenter.ProfileTitle, presenter.ProfileHint, presenter.ShowOnMap,
        ];
        Assert.All(titles, t => Assert.Matches(@"^\[.+\]$", t));
        Assert.Equal(15, titles.Distinct().Count());
    }

    [Theory]
    [InlineData(0, 5, 9, "5:09")]
    [InlineData(1, 5, 9, "1:05:09")]
    [InlineData(26, 0, 0, "26:00:00")]
    public void FormatoDeTiempo(int h, int m, int s, string expected) =>
        Assert.Equal(expected, RouteInfoPresenter.FormatTime(new TimeSpan(h, m, s)));

    // -----------------------------------------------------------------------
    // Estadisticas y perfil
    // -----------------------------------------------------------------------

    [Fact]
    public void ElRuidoDelGpsNoSumaDesnivelYLosSaltosNoSonVelocidad()
    {
        var points = new List<Location>
        {
            P(40, 0, 600), P(40.0001, 1, 601), P(40.0002, 2, 599), P(40.0003, 3, 600),   // ±1 m: ruido
            P(40.5, 4, 600),   // 55 km en un minuto: salto del GPS, no cuenta como movimiento
            new(40.5001, -3, 0) { Timestamp = default },   // sin hora ni altitud
        };

        var s = RouteStats.From(points);

        Assert.Equal((0.0, 0.0), (s.GainM, s.LossM));
        Assert.True(s.MaxSpeedKmh < 40);
        Assert.Equal(TimeSpan.FromMinutes(4), s.Duration);
        Assert.Equal(5, s.Profile.Count);
        Assert.False(RouteStats.From([]).HasElevation);
    }

    [Fact]
    public void ElPerfilSeDibujaConYSinDatos()
    {
        var canvas = new PictureCanvas(0, 0, 400, 220);
        var empty = new ProfileDrawable { NoData = "Sin altitud" };
        empty.Draw(canvas, new RectF(0, 0, 400, 220));

        foreach (var (km, dark) in new[] { (0.4, false), (2.0, true), (5.0, false), (12.0, true), (30.0, false) })
        {
            var drawable = new ProfileDrawable
            {
                Dark = dark,
                Profile = [(0, 600), (km / 2, 604), (km, 602)],   // desnivel minimo: se abre a ±5 m
            };
            drawable.Draw(canvas, new RectF(0, 0, 400, 220));
        }

        var flat = new ProfileDrawable { Profile = [(0, 600), (0, 900)] };   // todo en el km 0
        flat.Draw(canvas, new RectF(0, 0, 400, 220));

        Assert.NotNull(canvas.Picture);
    }

    // -----------------------------------------------------------------------
    // Menu lateral
    // -----------------------------------------------------------------------

    [Fact]
    public void TextosEInterruptoresDelMenu()
    {
        var shell = new ShellPresenter(p => p);
        var t = shell.Texts();
        Assert.Equal(new MenuTexts("Mapa", "Grabar", "Parar", "Seguir: Sí", "Rumbo: Sí", "Guardar", "Borrar", "Rutas", "Configuración", "Acerca de"), t);

        Assert.False(shell.ToggleFollow());
        Assert.False(shell.ToggleHeading());
        Assert.Equal(("Seguir: No", "Rumbo: No"), (shell.FollowText, shell.HeadingText));
        Assert.True(shell.ToggleFollow());
        Assert.Equal("Entendido", shell.Understood);
    }

    [Theory]
    [InlineData("map", "Mapa")]
    [InlineData("play", "Grabar")]
    [InlineData("stop", "Parar")]
    [InlineData("save", "Guardar")]
    [InlineData("clear", "Borrar")]
    [InlineData("follow", "Seguir")]
    [InlineData("heading", "Rumbo")]
    public void CadaAccionTieneSuExplicacion(string action, string title)
    {
        var (t, message) = new ShellPresenter(p => p).InfoFor(action);
        Assert.Equal(title, t);
        Assert.EndsWith(".", message);
    }

    [Fact]
    public void UnaAccionDesconocidaNoTieneExplicacion() =>
        Assert.Equal(("otra", ""), new ShellPresenter(p => p).InfoFor("otra"));

    [Theory]
    [InlineData(true, true, true, 3, false, BackAction.CloseFlyout)]
    [InlineData(false, true, true, 1, true, BackAction.Ignore)]
    [InlineData(false, true, false, 1, true, BackAction.DismissDialog)]
    [InlineData(false, false, false, 2, false, BackAction.PopPage)]
    [InlineData(false, false, false, 1, false, BackAction.GoHome)]
    [InlineData(false, false, false, 1, true, BackAction.MoveToBack)]
    public void OrdenDelBotonDeAtras(bool flyout, bool dialog, bool keep, int depth, bool home, BackAction expected) =>
        Assert.Equal(expected, ShellPresenter.DecideBack(flyout, dialog, keep, depth, home));
}

/// <summary>La hora de los puntos guardados (fallo encontrado por las pruebas el 2026-10-01).</summary>
public class RouteTimeTests : IDisposable
{
    public RouteTimeTests()
    {
        FileSystem.AppDataDirectory = Path.Combine(Path.GetTempPath(), $"hiker-time-{Guid.NewGuid():N}");
        Directory.CreateDirectory(FileSystem.AppDataDirectory);
    }

    public void Dispose()
    {
        try { Directory.Delete(FileSystem.AppDataDirectory, true); } catch (IOException) { }
    }

    [Fact]
    public async Task LaHoraSeGuardaEnUtcYSeLeeIgual()
    {
        var routes = new RouteService(new SettingsService());
        var at = new DateTimeOffset(2026, 7, 1, 12, 30, 0, TimeSpan.FromHours(2));   // 10:30 UTC
        var path = await routes.SaveRouteAsync([new Location(40, -3, 600) { Timestamp = at }], "Hora");

        Assert.Contains("2026-07-01T10:30:00Z", File.ReadAllText(path));
        Assert.Equal(at, (await routes.LoadRouteLocationsAsync("Hora"))[0].Timestamp);
    }

    [Fact]
    public void LasHorasSinZonaDeVersionesAnterioresSonUtc()
    {
        var old = new DateTime(2026, 7, 1, 10, 30, 0, DateTimeKind.Unspecified);
        Assert.Equal(new DateTimeOffset(2026, 7, 1, 10, 30, 0, TimeSpan.Zero), RouteService.ToTimestamp(old));

        var local = new DateTime(2026, 7, 1, 12, 30, 0, DateTimeKind.Local);
        Assert.Equal(new DateTimeOffset(local), RouteService.ToTimestamp(local));
        Assert.Equal(DateTimeOffset.MinValue, RouteService.ToTimestamp(DateTime.MinValue));   // sin hora: no revienta
    }
}
