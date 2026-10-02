using System.Globalization;
using Hiker.Helpers;
using Hiker.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Hiker.Tests;

/// <summary>GPS en vivo, comprobacion de version, registro de servicios, puntos de Android, GPX importados y seguimiento.</summary>
public class ServicesTests
{
    private static GeolocationService Geo(FakeGeolocation gps, FakePermission? permission = null, int interval = 1)
    {
        var settings = new SettingsService();
        settings.AppSettings.TimerInterval = interval;
        return new GeolocationService(settings, gps, permission ?? new FakePermission());
    }

    // -----------------------------------------------------------------------
    // GeolocationService
    // -----------------------------------------------------------------------

    [Fact]
    public async Task EscuchaElGpsYEntregaLasPosiciones()
    {
        var gps = new FakeGeolocation();
        await using var geo = Geo(gps, interval: 3);
        var received = new List<Location>();
        var got = new SemaphoreSlim(0);
        geo.LocationChanged += l => { received.Add(l); got.Release(); };

        Assert.True(await geo.ListeningStartAsync());
        Assert.True(await geo.ListeningStartAsync());   // ya escuchando
        Assert.Equal(("Native", true), (geo.NativeMode, geo.IsListening));
        Assert.Equal(TimeSpan.FromSeconds(3), gps.Request!.MinimumTime);
        Assert.Equal(GeolocationAccuracy.Medium, gps.Request.DesiredAccuracy);

        gps.Raise(new Location(40, -3));
        Assert.True(await got.WaitAsync(5000));
        Assert.Equal(40, received[0].Latitude);

        await geo.ListeningStopAsync();
        await geo.ListeningStopAsync();   // ya parado
        Assert.Equal(("Off", 1, 0), (geo.NativeMode, gps.Stops, gps.Subscribers));
    }

    [Fact]
    public async Task ElIntervaloTieneUnMinimoDeUnSegundo()
    {
        await using var geo = Geo(new FakeGeolocation(), interval: 0);
        Assert.Equal(TimeSpan.FromSeconds(1), geo.ListeningInterval);
    }

    [Fact]
    public async Task SinPermisoSePideYSiSeNiegaNoSeEscucha()
    {
        var gps = new FakeGeolocation();
        var permission = new FakePermission { Check = PermissionStatus.Denied, Request = PermissionStatus.Denied };
        await using var geo = Geo(gps, permission);

        Assert.False(await geo.ListeningStartAsync());
        Assert.Equal((1, 0), (permission.Requests, gps.Subscribers));

        permission.Request = PermissionStatus.Granted;
        Assert.True(await geo.ListeningStartAsync());
        Assert.Equal(2, permission.Requests);
    }

    [Fact]
    public async Task SiElGpsNoArrancaOFallaNoSeQuedaEscuchando()
    {
        var gps = new FakeGeolocation { StartResult = false };
        await using var geo = Geo(gps);
        Assert.False(await geo.ListeningStartAsync());
        Assert.Equal(0, gps.Subscribers);

        gps.StartThrows = new InvalidOperationException("sin GPS");
        Assert.False(await geo.ListeningStartAsync());
        Assert.Equal(("Off", 0), (geo.NativeMode, gps.Subscribers));
    }

    [Fact]
    public async Task UnaPosicionVaciaSeIgnoraYUnSuscriptorQueFallaNoParaLaCola()
    {
        var gps = new FakeGeolocation();
        await using var geo = Geo(gps);
        var got = new SemaphoreSlim(0);
        var calls = 0;
        geo.LocationChanged += _ => { if (++calls == 1) throw new InvalidOperationException("pagina rota"); got.Release(); };
        await geo.ListeningStartAsync();

        gps.Raise(new Location(1, 1));
        gps.Raise(new Location(2, 2));
        Assert.True(await got.WaitAsync(5000));
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task ElFixActualCaeALaUltimaConocidaYEstaANull()
    {
        var gps = new FakeGeolocation { Fix = new Location(1, 1), LastKnown = new Location(2, 2) };
        await using var geo = Geo(gps);

        Assert.Equal(1, (await geo.GetCurrentLocationAsync())!.Latitude);
        gps.Fix = null;
        Assert.Equal(2, (await geo.GetCurrentLocationAsync())!.Latitude);
        gps.FixThrows = new TimeoutException();
        Assert.Equal(2, (await geo.GetCurrentLocationAsync())!.Latitude);
        gps.LastKnownThrows = new InvalidOperationException();
        Assert.Null(await geo.GetCurrentLocationAsync());
        Assert.Null(await geo.GetLastKnownLocationAsync());
    }

    [Fact]
    public async Task AlCerrarloDejaDeEscucharYNoContestaMas()
    {
        var gps = new FakeGeolocation { Fix = new Location(1, 1), LastKnown = new Location(2, 2) };
        var geo = Geo(gps);
        await geo.ListeningStartAsync();

        geo.Dispose();
        geo.Dispose();

        Assert.Equal((1, 0), (gps.Stops, gps.Subscribers));
        Assert.False(await geo.ListeningStartAsync());
        Assert.Null(await geo.GetCurrentLocationAsync());
        Assert.Null(await geo.GetLastKnownLocationAsync());
        await geo.ListeningStopAsync();
        gps.Raise(new Location(3, 3));   // llega tarde: no pasa nada
    }

    // -----------------------------------------------------------------------
    // UpdateService
    // -----------------------------------------------------------------------

    private static UpdateService Updates(FakeLinks links, string json, string current = "2026.10.01.00") =>
        new(new TranslationService(new SettingsService()), links, () => Task.FromResult(json), () => current);

    [Fact]
    public async Task ConVersionNuevaPreguntaYAbreElEnlaceUnaSolaVez()
    {
        var links = new FakeLinks();
        var dialogs = new FakeDialogs();
        dialogs.Confirms.Enqueue(true);
        var updates = Updates(links, """{"version":"2026.10.2.0","url":"https://example.org/hiker"}""");

        await updates.CheckAndPromptAsync(dialogs);
        await updates.CheckAndPromptAsync(dialogs);

        Assert.Single(dialogs.Shown);
        Assert.Contains("2026.10.2.0", dialogs.Shown[0]);
        Assert.Equal(["https://example.org/hiker"], links.Opened);
    }

    [Theory]
    [InlineData("""{"version":"2026.10.01.00","url":"x"}""")]   // al dia
    [InlineData("""{"version":"2025.1.1","url":"x"}""")]        // mas vieja
    [InlineData("""{"url":"x"}""")]                              // sin version
    [InlineData("no es json")]
    public async Task SinVersionNuevaNoMolesta(string json)
    {
        var dialogs = new FakeDialogs();
        await Updates(new FakeLinks(), json).CheckAndPromptAsync(dialogs);
        Assert.Empty(dialogs.Shown);
    }

    [Fact]
    public async Task SiNoQuiereActualizarOFaltaElEnlaceNoSeAbreNada()
    {
        var links = new FakeLinks();
        await Updates(links, """{"version":"2099.1"}""").CheckAndPromptAsync(new FakeDialogs());
        var yes = new FakeDialogs();
        yes.Confirms.Enqueue(true);
        await Updates(links, """{"version":"2099.1","url":" "}""").CheckAndPromptAsync(yes);
        Assert.Empty(links.Opened);
    }

    [Theory]
    [InlineData("2026.10.1.0", "2026.9.30.0", 1)]
    [InlineData("2026.10", "2026.10.0.0", 0)]
    [InlineData("2026.x.1", "2026.0.2", -1)]
    public void CompararVersiones(string a, string b, int sign) =>
        Assert.Equal(sign, Math.Sign(UpdateService.CompareVersions(a, b)));

    // -----------------------------------------------------------------------
    // Registro de servicios
    // -----------------------------------------------------------------------

    [Fact]
    public void TodosLosServiciosSeResuelvenYSonUnicos()
    {
        var provider = new ServiceCollection().AddHikerServices()
            .AddSingleton<ILinkOpener, FakeLinks>()
            .BuildServiceProvider();

        Assert.Same(provider.GetRequiredService<GeolocationService>(), provider.GetRequiredService<ILocationSource>());
        Assert.Same(provider.GetRequiredService<TrackRecorder>(), provider.GetRequiredService<TrackRecorder>());
        Assert.NotNull(provider.GetRequiredService<UpdateService>());
        Assert.NotNull(provider.GetRequiredService<MapMatchService>());
        Assert.NotNull(provider.GetRequiredService<RouteService>());
        Assert.NotNull(provider.GetRequiredService<ICompass>());
        Assert.NotNull(provider.GetRequiredService<Microsoft.Maui.Storage.IPreferences>());
        Assert.IsType<EssentialsLocationPermission>(provider.GetRequiredService<ILocationPermission>());
        Assert.Equal(TimeSpan.FromSeconds(30), provider.GetRequiredService<HttpClient>().Timeout);
    }

    [Fact]
    public void SinServicioEnPrimerPlanoNoPasaNada()
    {
        var none = new NoTrackingService();
        none.Start("Hiker", "Grabando");
        none.Stop();
        // Fuera del movil, el permiso de verdad no existe: lo dice en vez de inventarse uno.
        Assert.ThrowsAny<Exception>(() => new EssentialsLocationPermission().CheckAsync().GetAwaiter().GetResult());
        Assert.ThrowsAny<Exception>(() => new EssentialsLocationPermission().RequestAsync().GetAwaiter().GetResult());
    }

    // -----------------------------------------------------------------------
    // Puntos del servicio en primer plano
    // -----------------------------------------------------------------------

    [Fact]
    public void ElPuntoDeAndroidConservaLoQueTraeYDejaVacioLoQueNo()
    {
        var full = TrackPointFactory.Create(40, -3, 700, 4.5, 1.2, 90, 1_790_000_000_000);
        Assert.Equal((40.0, -3.0, 700.0, 4.5, 1.2, 90.0), (full.Latitude, full.Longitude, full.Altitude, full.Accuracy, full.Speed, full.Course));
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1_790_000_000_000), full.Timestamp);

        var bare = TrackPointFactory.Create(40, -3, null, null, null, null, 0);
        Assert.Equal((0.0, (double?)null, (double?)null, (double?)null), (bare.Altitude ?? -1, bare.Accuracy, bare.Speed, bare.Course));
    }

    [Theory]
    [InlineData(null, 1000)]
    [InlineData(0, 1000)]
    [InlineData(5, 5000)]
    public void IntervaloDelServicio(int? seconds, long expected) =>
        Assert.Equal(expected, TrackPointFactory.IntervalMilliseconds(seconds));

    // -----------------------------------------------------------------------
    // GPX importados
    // -----------------------------------------------------------------------

    [Fact]
    public void GpxDeCualquierVersionYSinHoras()
    {
        var gpx10 = """
            <gpx version="1.0" xmlns="http://www.topografix.com/GPX/1/0">
              <trk><trkseg>
                <trkpt lat="40.1" lon="-3.1"><ele>650.5</ele><time>2026-09-27T10:00:00Z</time></trkpt>
                <trkpt lat="40.2" lon="-3.2"></trkpt>
                <trkpt lat="no" lon="-3.3"></trkpt>
                <trkpt lon="-3.3"></trkpt>
              </trkseg></trk>
              <wpt lat="1" lon="1" />
            </gpx>
            """;
        var points = GpxPointReader.Read(gpx10);

        Assert.Equal(2, points.Count);   // los puntos sueltos no cuentan si hay track; los rotos se saltan
        Assert.Equal((40.1, -3.1, 650.5), (points[0].Latitude, points[0].Longitude, points[0].Altitude));
        Assert.Equal(new DateTimeOffset(2026, 9, 27, 10, 0, 0, TimeSpan.Zero), points[0].Timestamp);
        Assert.Null(points[1].Altitude);
    }

    [Fact]
    public void SinTrackSeUsanLaRutaOLosPuntosSueltos()
    {
        Assert.Equal(2, GpxPointReader.Read("""<gpx><rte><rtept lat="1" lon="1"/><rtept lat="2" lon="2"/></rte><wpt lat="3" lon="3"/></gpx>""").Count);
        Assert.Equal(3, GpxPointReader.Read("""<gpx><wpt lat="3" lon="3"/></gpx>""")[0].Latitude);
        Assert.Empty(GpxPointReader.Read("<gpx/>"));
        Assert.ThrowsAny<System.Xml.XmlException>(() => GpxPointReader.Read("<gpx"));
    }

    // -----------------------------------------------------------------------
    // Seguimiento
    // -----------------------------------------------------------------------

    [Fact]
    public void SeguirUnaRuta()
    {
        var follower = new RouteFollower(p => p);
        Assert.Null(follower.Update(new Location(40, -3)));
        Assert.Equal(0, follower.TotalMeters);

        var start = follower.Start([new Location(40, -3), new Location(40.009, -3), new Location(40.018, -3)]);
        Assert.Equal("Siguiendo la ruta", start.Title);
        Assert.InRange(follower.TotalMeters, 1990, 2010);

        var onIt = follower.Update(new Location(40.0, -3.0))!;
        Assert.False(onIt.OffRoute);
        Assert.StartsWith("Quedan 2", onIt.Detail);

        var end = follower.Update(new Location(40.018, -3.0005))!;
        Assert.StartsWith("Quedan 0 m", end.Detail);

        follower.Stop();
        Assert.False(follower.IsFollowing);
    }

    [Fact]
    public void DistanciasEnMetrosOKilometros()
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("es-ES");
        try
        {
            Assert.Equal("850 m", RouteFollower.FormatDistance(849.6));
            Assert.Equal("1,2 km", RouteFollower.FormatDistance(1234));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
