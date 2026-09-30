using System.Net;
using System.Text;
using Hiker.Helpers;
using Hiker.Services;

namespace Hiker.Tests;

/// <summary>Estadisticas de una ruta, guardar/listar/borrar GPX y el ajuste al mapa.</summary>
public class RouteTests : IDisposable
{
    private static readonly DateTime T0 = new(2026, 9, 27, 10, 0, 0, DateTimeKind.Utc);

    public RouteTests()
    {
        FileSystem.AppDataDirectory = Path.Combine(Path.GetTempPath(), $"hiker-routes-{Guid.NewGuid():N}");
        Directory.CreateDirectory(FileSystem.AppDataDirectory);
    }

    public void Dispose()
    {
        try { Directory.Delete(FileSystem.AppDataDirectory, true); } catch (IOException) { }
    }

    private static RouteService Service() => new(new SettingsService());

    /// <summary>Tres puntos hacia el norte, ~1 km cada tramo, subiendo y bajando.</summary>
    private static string Gpx(string name = "Subida") => new GPXFileHelper
    {
        Creator = "prueba",
        Tracks =
        {
            new Track
            {
                Name = name,
                Segments =
                {
                    new TrackSegment
                    {
                        TrackPoints =
                        {
                            new TrackPoint(40.000, -3, 600, T0),
                            new TrackPoint(40.009, -3, 900, T0.AddMinutes(15)),
                            new TrackPoint(40.018, -3, 700, T0.AddMinutes(40)),
                        },
                    },
                },
            },
        },
    }.ToXML();

    [Fact]
    public async Task EstadisticasDeUnGpx()
    {
        var service = Service();
        var data = await service.SetRoute(Gpx());

        Assert.Equal("Subida", data.routeName);
        Assert.InRange(data.totalDistance, 1.99, 2.01);
        Assert.Equal((600.0, 900.0, 300.0), (data.minElevation, data.maxElevation, data.desnivel));
        Assert.Equal(TimeSpan.FromMinutes(40), data.estimatedTime);
        Assert.Equal(0, data.stepDistance);   // (int)(2 km / 10)
        Assert.Equal(3, data.elevationData.Count);
        Assert.Equal(0, data.elevationData[0].Distance);
        Assert.InRange(data.elevationData[2].Distance, 1.99, 2.01);
        Assert.Equal(700, data.elevationData[2].Elevation);
        Assert.Equal(Gpx(), service.GetRoute());
    }

    [Fact]
    public async Task GpxSinTrackDaUnaRutaVaciaYElIlegibleNull()
    {
        var empty = await Service().SetRoute(new GPXFileHelper().ToXML());
        Assert.Equal(string.Empty, empty.routeName);
        Assert.Empty(empty.elevationData);

        Assert.Null(await Service().SetRoute("esto no es un gpx"));
    }

    [Fact]
    public async Task GrabarUnaRutaEnGpxYVolverALeerla()
    {
        var service = Service();
        var points = new List<Location>
        {
            new(40, -3, 650) { Timestamp = new DateTimeOffset(T0) },
            new(40.009, -3, 700) { Timestamp = new DateTimeOffset(T0.AddMinutes(10)) },
        };
        await service.SetLocations(points);

        var data = await service.GetRouteData();
        Assert.InRange(data.totalDistance, 0.99, 1.01);
        Assert.Equal(50, data.desnivel);

        using var stream = await service.SaveTrack("Paseo");
        var back = GPXFileHelper.FromStream(stream);
        Assert.Equal("Paseo", back.Tracks[0].Name);
        Assert.Equal(2, back.Tracks[0].Segments[0].TrackPoints.Count);
        Assert.Equal("Hiker", (await service.GetGPXFileHelper()).Creator);
    }

    [Fact]
    public async Task GuardarListarCargarYBorrarRutas()
    {
        var service = Service();
        var points = new List<Location>
        {
            new(40, -3, 650) { Timestamp = new DateTimeOffset(T0) },
            new(40.009, -3, 700) { Timestamp = new DateTimeOffset(T0.AddMinutes(10)) },
        };

        var path = await service.SaveRouteAsync(points, "Ruta del río");
        Assert.Equal("Ruta_del_río.gpx", Path.GetFileName(path));
        Assert.Equal(Path.Combine(FileSystem.AppDataDirectory, "routes"), RouteService.RoutesDirectory);
        await File.WriteAllTextAsync(Path.Combine(RouteService.RoutesDirectory, "rota.gpx"), "no es xml");

        var all = await service.GetAllRoutesAsync();
        var route = Assert.Single(all);   // la ilegible no sale
        Assert.Equal("Ruta_del_río", route.routeName);
        Assert.InRange(route.totalDistance, 0.99, 1.01);

        var loaded = await service.LoadRouteLocationsAsync("Ruta del río");
        Assert.Equal(2, loaded.Count);
        Assert.Equal((40.009, 700.0), (loaded[1].Latitude, loaded[1].Altitude!.Value));
        Assert.Equal(File.GetLastWriteTime(path), service.GetRouteDate("Ruta del río"));

        await service.DeleteRouteAsync("Ruta del río");
        Assert.Empty(await service.LoadRouteLocationsAsync("Ruta del río"));
        Assert.True(DateTime.Now - service.GetRouteDate("Ruta del río") < TimeSpan.FromMinutes(1));   // sin fichero: ahora
        await service.DeleteRouteAsync("no existe");   // no lanza
    }

    [Fact]
    public async Task FicheroBloqueadoSeSaltaAlListarYAlBorrarAvisa()
    {
        var service = Service();
        var points = new List<Location> { new(40, -3, 650) { Timestamp = new DateTimeOffset(T0) }, new(40.001, -3, 650) { Timestamp = new DateTimeOffset(T0) } };
        var path = await service.SaveRouteAsync(points, "bloqueada");
        await service.SaveRouteAsync(points, "libre");

        using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.Equal("libre", Assert.Single(await service.GetAllRoutesAsync()).routeName);
            await Assert.ThrowsAsync<IOException>(() => service.DeleteRouteAsync("bloqueada"));
        }

        Assert.Equal(2, (await service.GetAllRoutesAsync()).Count);
    }

    [Fact]
    public async Task GuardarSinPuntosNoSePuede()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service().SaveRouteAsync([], "vacia"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service().SaveRouteAsync(null!, "vacia"));
    }

    [Fact]
    public void RutaPendienteDeCargar()
    {
        RouteService.PendingRouteToLoad = "x";
        Assert.Equal("x", RouteService.PendingRouteToLoad);
        RouteService.PendingRouteToLoad = null;
    }

    // -----------------------------------------------------------------------
    // Ajuste al mapa
    // -----------------------------------------------------------------------

    private sealed class Overpass(Func<string, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<string> Hosts { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Hosts.Add(request.RequestUri!.Host);
            return Task.FromResult(respond(request.RequestUri.Host));
        }
    }

    private static HttpResponseMessage Json(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    /// <summary>Un camino en la latitud 40,0000 y un edificio cuadrado de ~110 m al norte.</summary>
    private const string Map = """
        {"elements":[
          {"type":"way","geometry":[{"lat":40.0000,"lon":-3.010},{"lat":40.0000,"lon":-2.990}],"tags":{"highway":"path"}},
          {"type":"way","geometry":[{"lat":40.005,"lon":-3.001},{"lat":40.005,"lon":-2.999},{"lat":40.006,"lon":-2.999},{"lat":40.006,"lon":-3.001},{"lat":40.005,"lon":-3.001}],"tags":{"building":"yes"}},
          {"type":"way","geometry":[{"lat":41,"lon":-3}]},
          {"type":"node","lat":40,"lon":-3},
          {"type":"relation","members":[]},
          {"type":"way","geometry":[{"lat":1},{"lon":2}]}
        ]}
        """;

    private static Location L(double lat, double lon) => new(lat, lon) { Timestamp = new DateTimeOffset(T0), Altitude = 700, Accuracy = 6 };

    [Fact]
    public async Task PegaAlCaminoSoloLoCercanoYSacaDeLosEdificios()
    {
        var service = new MapMatchService(new HttpClient(new Overpass(_ => Json(Map))));
        var track = new List<Location>
        {
            L(40.0001, -3.000),    // 11 m del camino: se pega
            L(40.0020, -3.000),    // 220 m: campo a traves, se respeta
            L(40.0055, -3.0002),   // dentro del edificio: sale al borde
        };

        var result = await service.MatchAsync(track);

        Assert.NotNull(result);
        Assert.Equal((1, 1, 2, true), (result.SnappedToPath, result.MovedOutOfBuilding, result.Changed, result.AnyChange));
        Assert.Equal(40.0, result.Points[0].Latitude, 7);
        Assert.Equal(40.0020, result.Points[1].Latitude, 9);
        var moved = result.Points[2];
        Assert.True(moved.Latitude < 40.005 || moved.Latitude > 40.006 || moved.Longitude < -3.001 || moved.Longitude > -2.999,
            $"sigue dentro: {moved.Latitude} {moved.Longitude}");
        Assert.Equal((700.0, (double?)6, new DateTimeOffset(T0)), (moved.Altitude!.Value, moved.Accuracy, moved.Timestamp));
    }

    [Fact]
    public async Task SiElPrimerServidorEstaSaturadoPruebaElSegundo()
    {
        var handler = new Overpass(host => host == "overpass-api.de" ? new HttpResponseMessage(HttpStatusCode.TooManyRequests) : Json("""{"elements":[]}"""));
        var result = await new MapMatchService(new HttpClient(handler)).MatchAsync([L(40, -3), L(40.001, -3)]);

        Assert.Equal(["overpass-api.de", "overpass.kumi.systems"], handler.Hosts);
        Assert.False(result!.AnyChange);
        Assert.Equal(2, result.Points.Count);
    }

    [Fact]
    public async Task SinMapaDevuelveNullParaDistinguirloDeNadaQueCorregir()
    {
        var handler = new Overpass(_ => throw new HttpRequestException("sin red"));
        Assert.Null(await new MapMatchService(new HttpClient(handler)).MatchAsync([L(40, -3), L(40.001, -3)]));
        Assert.Equal(2, handler.Hosts.Count);

        var rara = new Overpass(_ => Json("""{"version":0.6}"""));   // sin elements: no hay nada
        Assert.False((await new MapMatchService(new HttpClient(rara)).MatchAsync([L(40, -3), L(40.001, -3)]))!.AnyChange);
    }

    [Fact]
    public async Task UnSoloPuntoNoPregunta()
    {
        var handler = new Overpass(_ => Json(Map));
        var result = await new MapMatchService(new HttpClient(handler)).MatchAsync([L(40, -3)]);
        Assert.Single(result!.Points);
        Assert.Empty(handler.Hosts);
    }

    [Fact]
    public async Task CaminoConTramoDegeneradoYPuntoJustoEnElBorde()
    {
        const string map = """
            {"elements":[
              {"type":"way","geometry":[{"lat":40.0,"lon":-3.0},{"lat":40.0,"lon":-3.0},{"lat":40.0,"lon":-2.99}]}
            ]}
            """;
        var result = await new MapMatchService(new HttpClient(new Overpass(_ => Json(map)))).MatchAsync([L(40.0, -3.0), L(40.00005, -2.995)]);

        Assert.Equal(2, result!.SnappedToPath);
        Assert.Equal(-2.995, result.Points[1].Longitude, 6);
    }
}
