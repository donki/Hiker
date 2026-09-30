using System.Globalization;
using Hiker.Services;

namespace Hiker.Tests;

/// <summary>Filtros del GPS y grabacion de la ruta con su diario en disco.</summary>
public class RecordingTests : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 27, 10, 0, 0, TimeSpan.Zero);

    public RecordingTests()
    {
        FileSystem.AppDataDirectory = Path.Combine(Path.GetTempPath(), $"hiker-rec-{Guid.NewGuid():N}");
        Directory.CreateDirectory(FileSystem.AppDataDirectory);
        Preferences.Values.Clear();
    }

    public void Dispose()
    {
        try { Directory.Delete(FileSystem.AppDataDirectory, true); } catch (IOException) { }
    }

    private static string Journal => Path.Combine(FileSystem.AppDataDirectory, "recording.csv");

    /// <summary>Ajustes con solo los filtros pedidos.</summary>
    private static SettingsService Settings(bool kalman = false, bool average = false, bool speed = false, bool accuracy = false,
        double maxSpeed = 3, int maxAccuracy = 10, int window = 3)
    {
        var s = new SettingsService();
        s.AppSettings.KalmanFilterEnabled = kalman;
        s.AppSettings.AverageFilterEnabled = average;
        s.AppSettings.SpeedFilterEnabled = speed;
        s.AppSettings.AccuracyFilterEnabled = accuracy;
        s.AppSettings.MaxSpeed = maxSpeed;
        s.AppSettings.Accuracy = maxAccuracy;
        s.AppSettings.WindowSize = window;
        return s;
    }

    private static Location P(double lat, double lon, double seconds = 0, double? acc = 5, double? alt = null) =>
        new(lat, lon) { Timestamp = T0.AddSeconds(seconds), Accuracy = acc, Altitude = alt };

    // -----------------------------------------------------------------------
    // Filtros
    // -----------------------------------------------------------------------

    [Fact]
    public void SinFiltrosPasaTalCual()
    {
        using var filter = new GpsFilterService(Settings());
        var p = filter.ProcessLocation(P(40, -3));
        Assert.Equal((40.0, -3.0), (p!.Latitude, p.Longitude));
    }

    [Fact]
    public void FiltroDePrecisionDescartaLasMalas()
    {
        using var filter = new GpsFilterService(Settings(accuracy: true, maxAccuracy: 10));
        Assert.Null(filter.ProcessLocation(P(40, -3, acc: 25)));
        Assert.NotNull(filter.ProcessLocation(P(40, -3, acc: 8)));
        Assert.NotNull(filter.ProcessLocation(P(40, -3, acc: null)));   // sin dato de precision, pasa
    }

    [Fact]
    public void FiltroDeVelocidadDescartaLosSaltos()
    {
        using var filter = new GpsFilterService(Settings(speed: true, maxSpeed: 3));
        Assert.NotNull(filter.ProcessLocation(P(40, -3, 0)));
        Assert.Null(filter.ProcessLocation(P(40.01, -3, 10)));       // 1,1 km en 10 s
        Assert.NotNull(filter.ProcessLocation(P(40.0001, -3, 10)));  // 11 m en 10 s
    }

    [Fact]
    public void KalmanSuavizaYConservaElRestoDeDatos()
    {
        using var filter = new GpsFilterService(Settings(kalman: true));
        filter.ProcessLocation(P(40, -3, 0, acc: 5));
        var p = filter.ProcessLocation(P(40.001, -3, 0, acc: 5, alt: 700))!;

        Assert.InRange(p.Latitude, 40.0001, 40.0009);   // entre la anterior y la nueva
        Assert.Equal(700, p.Altitude);
        Assert.Equal(T0, p.Timestamp);
    }

    /// <summary>
    /// Con lecturas separadas un segundo y la misma precision, el Kalman tiene que quedarse a medio
    /// camino. (Fallo encontrado: se le pasaban ticks de 100 ns como si fueran milisegundos, la
    /// incertidumbre crecia 10 000 veces mas deprisa y el filtro no suavizaba nada.)
    /// </summary>
    [Fact]
    public void KalmanSuavizaEntreLecturasSeparadasEnElTiempo()
    {
        using var filter = new GpsFilterService(Settings(kalman: true));
        filter.ProcessLocation(P(40, -3, 0, acc: 5));
        var p = filter.ProcessLocation(P(40.001, -3, 1, acc: 5))!;

        // Q = 3 m/s: varianza 25 + 1000 ms * 9 / 1000 = 34; K = 34 / (34 + 25) = 0,576.
        Assert.Equal(40 + 0.001 * 34.0 / 59.0, p.Latitude, 6);
    }

    [Fact]
    public void PromedioMovilConVentanaYAltitud()
    {
        using var filter = new GpsFilterService(Settings(average: true, window: 2));
        Assert.Equal(40, filter.ProcessLocation(P(40, -3, alt: 100))!.Latitude);
        var second = filter.ProcessLocation(P(40.002, -3, alt: null))!;
        Assert.Equal(40.001, second.Latitude, 9);
        Assert.Equal(100, second.Altitude);                            // media de las que tienen altitud
        var third = filter.ProcessLocation(P(40.004, -3, alt: 300))!;   // ventana de 2: sale la primera
        Assert.Equal(40.003, third.Latitude, 9);
        Assert.Equal(300, third.Altitude);

        var sinAltitud = new GpsFilterService(Settings(average: true, window: 2));
        sinAltitud.ProcessLocation(P(40, -3));
        Assert.Equal(0, sinAltitud.ProcessLocation(P(40.002, -3))!.Altitude);
    }

    [Fact]
    public void VentanaAcotadaEntreUnoYDiez()
    {
        using var filter = new GpsFilterService(Settings(average: true, window: 50));
        for (var i = 0; i < 20; i++)
            filter.ProcessLocation(P(40 + i * 0.001, -3));
        // Ventana de 10 como mucho: i = 11..19 y la nueva (40.020).
        Assert.Equal(40.0155, filter.ProcessLocation(P(40.020, -3))!.Latitude, 6);
    }

    [Fact]
    public async Task AplicarAVariasAsincronoYReiniciar()
    {
        var filter = new GpsFilterService(Settings(accuracy: true, average: true, window: 3));
        var kept = filter.ApplyFilters([P(40, -3), P(40.001, -3, acc: 99), P(40.002, -3)]);
        Assert.Equal(2, kept.Count);

        await filter.ResetAsync();
        var p = await filter.ProcessLocationAsync(P(41, -3));
        Assert.Equal(41, p!.Latitude);   // tras reiniciar, la media empieza de cero

        Assert.Null(await filter.ProcessLocationAsync(null!));
        Assert.Null(filter.ProcessLocation(null!));

        await filter.DisposeAsync();
        await filter.DisposeAsync();   // dos veces no pasa nada
        Assert.Null(filter.ProcessLocation(P(40, -3)));
        Assert.Null(await filter.ProcessLocationAsync(P(40, -3)));
        Assert.Empty(filter.ApplyFilters([P(40, -3)]));
        await filter.ResetAsync();   // no lanza
    }

    [Fact]
    public void LosAjustesNuevosSeLeenAlReiniciar()
    {
        var settings = Settings(accuracy: false);
        using var filter = new GpsFilterService(settings);
        Assert.NotNull(filter.ProcessLocation(P(40, -3, acc: 50)));

        settings.AppSettings.AccuracyFilterEnabled = true;
        filter.Reset();
        Assert.Null(filter.ProcessLocation(P(40, -3, acc: 50)));
    }

    // -----------------------------------------------------------------------
    // Grabacion
    // -----------------------------------------------------------------------

    [Fact]
    public void SinGrabarNoEntraNada()
    {
        var recorder = new TrackRecorder(new GpsFilterService(Settings()));
        Assert.False(recorder.Push(P(40, -3)));
        Assert.Equal(0, recorder.PointCount);
    }

    [Fact]
    public void GrabarAcumulaDistanciaEscribeElDiarioYAvisa()
    {
        var recorder = new TrackRecorder(new GpsFilterService(Settings()));
        var added = new List<Location>();
        var changes = 0;
        recorder.PointAdded += (_, p) => added.Add(p);
        recorder.RecordingChanged += (_, _) => changes++;

        recorder.Start();
        Assert.True(recorder.IsRecording);
        Assert.True(recorder.Push(P(40, -3, 0, alt: 650)));
        Assert.True(recorder.Push(P(40.009, -3, 60)));   // ~1 km al norte

        Assert.Equal(2, recorder.PointCount);
        Assert.Equal(2, added.Count);
        Assert.InRange(recorder.DistanceKm, 0.99, 1.01);
        Assert.Equal(2, recorder.Snapshot().Count);

        var lines = File.ReadAllLines(Journal);
        Assert.Equal(2, lines.Length);
        Assert.Equal(string.Create(CultureInfo.InvariantCulture, $"{T0.UtcTicks},40,-3,650,5"), lines[0]);

        recorder.Stop();
        Assert.False(recorder.IsRecording);
        Assert.False(recorder.Push(P(40.01, -3, 120)));
        Assert.Equal(2, changes);
    }

    [Fact]
    public void PuntoQueElFiltroDescartaNoCuenta()
    {
        var recorder = new TrackRecorder(new GpsFilterService(Settings(accuracy: true, maxAccuracy: 10)));
        recorder.Start();
        Assert.False(recorder.Push(P(40, -3, acc: 80)));
        Assert.Equal(0, recorder.PointCount);
    }

    [Fact]
    public void EmpezarOtraRutaBorraLaAnteriorYElDiario()
    {
        var recorder = new TrackRecorder(new GpsFilterService(Settings()));
        recorder.Start();
        recorder.Push(P(40, -3));
        recorder.Start();

        Assert.Equal(0, recorder.PointCount);
        Assert.Equal(0, recorder.DistanceKm);
        Assert.False(File.Exists(Journal));
    }

    [Fact]
    public void DescartarBorraPuntosYDiario()
    {
        var recorder = new TrackRecorder(new GpsFilterService(Settings()));
        recorder.Start();
        recorder.Push(P(40, -3));
        recorder.Push(P(40.001, -3, 10));

        recorder.Discard();

        Assert.Equal(0, recorder.PointCount);
        Assert.False(File.Exists(Journal));
    }

    [Fact]
    public void SiAndroidMataElProcesoSeRecuperaLaRutaDelDiario()
    {
        var before = new TrackRecorder(new GpsFilterService(Settings()));
        before.Start();
        before.Push(P(40, -3, 0, alt: 600));
        before.Push(P(40.009, -3, 60, alt: 610));
        // ... el sistema mata el proceso: se crea otro grabador.

        var after = new TrackRecorder(new GpsFilterService(Settings()));
        Assert.True(after.ResumeIfInterrupted());
        Assert.True(after.IsRecording);
        Assert.Equal(2, after.PointCount);
        Assert.InRange(after.DistanceKm, 0.99, 1.01);
        Assert.Equal(T0.LocalDateTime, after.StartedAt);
        var p = after.Snapshot()[1];
        Assert.Equal((40.009, 610.0, (double?)5, T0.AddSeconds(60)), (p.Latitude, p.Altitude!.Value, p.Accuracy, p.Timestamp));

        Assert.False(after.ResumeIfInterrupted());   // ya grabando
        TrackRecorder.DeletePendingJournal();
        Assert.Empty(TrackRecorder.ReadPendingJournal());
        Assert.False(new TrackRecorder(new GpsFilterService(Settings())).ResumeIfInterrupted());
    }

    [Fact]
    public void DiarioConLineasRotasSeSaltaLoIlegible()
    {
        File.WriteAllLines(Journal,
        [
            $"{T0.UtcTicks},40.5,-3.5,700,0",
            "basura",
            "x,40,-3",
            $"{T0.UtcTicks},no,-3",
            $"{T0.UtcTicks},40,no",
            $"{T0.AddSeconds(5).UtcTicks},40.6,-3.6",   // sin altitud ni precision
        ]);

        var points = TrackRecorder.ReadPendingJournal();

        Assert.Equal(2, points.Count);
        Assert.Null(points[0].Accuracy);   // precision 0 = sin dato
        Assert.Equal(0, points[1].Altitude);
    }

    [Fact]
    public void SinCarpetaParaElDiarioLaGrabacionSigueEnMemoria()
    {
        FileSystem.AppDataDirectory = Path.Combine(FileSystem.AppDataDirectory, "no", "existe");
        var recorder = new TrackRecorder(new GpsFilterService(Settings()));
        recorder.Start();
        Assert.True(recorder.Push(P(40, -3)));
        Assert.Equal(1, recorder.PointCount);
        Assert.Empty(TrackRecorder.ReadPendingJournal());
    }

    [Fact]
    public void DiarioBloqueadoNoRompeLaGrabacion()
    {
        File.WriteAllText(Journal, $"{T0.UtcTicks},40,-3,0,5");
        using (new FileStream(Journal, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.Empty(TrackRecorder.ReadPendingJournal());   // no se puede leer: nada pendiente
            var recorder = new TrackRecorder(new GpsFilterService(Settings()));
            recorder.Start();                                   // no se puede borrar: sigue
            Assert.True(recorder.Push(P(40, -3)));              // ni escribir: queda en memoria
            Assert.Equal(1, recorder.PointCount);
        }
    }

    [Fact]
    public void AdoptarVacioEmpiezaAhora()
    {
        var recorder = new TrackRecorder(new GpsFilterService(Settings()));
        recorder.Adopt([]);
        Assert.Equal(0, recorder.DistanceKm);
        Assert.True(DateTime.Now - recorder.StartedAt < TimeSpan.FromMinutes(1));
    }
}
