using System.Globalization;
using System.Text;
using Hiker.Helpers;

namespace Hiker.Tests;

/// <summary>GPX, nombres de fichero, formato de numeros y el filtro de Kalman.</summary>
public class HelpersTests
{
    private static string SampleGpx => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Ruta_Cortijo.gpx"));

    [Fact]
    public void GpxDeGpxStudioSeLeeEntero()
    {
        var gpx = GPXFileHelper.FromXML(SampleGpx);

        var track = Assert.Single(gpx.Tracks);
        var points = track.Segments.SelectMany(s => s.TrackPoints).ToList();
        Assert.Equal(224, points.Count);
        Assert.All(points, p => Assert.InRange(p.Latitude, -90, 90));
        Assert.Contains(points, p => p.Elevation != 0);
    }

    [Fact]
    public void GpxIdaYVueltaConservaPuntosAltitudYHora()
    {
        var at = new DateTime(2026, 9, 27, 10, 0, 0, DateTimeKind.Utc);
        var gpx = new GPXFileHelper { Creator = "Hiker" };
        var track = new Track { Name = "Cortijo" };
        track.Segments.Add(new TrackSegment
        {
            TrackPoints = { new TrackPoint(40.5, -3.25, 812.5, at), new TrackPoint(40.6, -3.3, null, at.AddMinutes(1)) },
        });
        gpx.Tracks.Add(track);

        var xml = gpx.ToXML();
        Assert.Contains("http://www.topografix.com/GPX/1/1", xml);
        Assert.Contains("version=\"1.1\"", xml);
        Assert.StartsWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>", xml);   // no «utf-16»: se guarda en UTF-8

        foreach (var back in new[] { GPXFileHelper.FromXML(xml), GPXFileHelper.FromStream(gpx.ToStream()), GPXFileHelper.FromStream(gpx.ToStream(gpx)) })
        {
            Assert.Equal("Hiker", back.Creator);
            var t = Assert.Single(back.Tracks);
            Assert.Equal("Cortijo", t.Name);
            var p = t.Segments[0].TrackPoints;
            Assert.Equal((40.5, -3.25, 812.5, at), (p[0].Latitude, p[0].Longitude, p[0].Elevation, p[0].Time));
            Assert.Equal(0, p[1].Elevation);   // sin altitud se escribe 0
        }
    }

    [Fact]
    public void GpxMalFormadoLanza()
    {
        Assert.ThrowsAny<InvalidOperationException>(() => GPXFileHelper.FromXML("<gpx><trk>"));
        Assert.ThrowsAny<InvalidOperationException>(() => GPXFileHelper.FromStream(new MemoryStream(Encoding.UTF8.GetBytes("no es xml"))));
    }

    [Theory]
    [InlineData("Ruta al Cortijo", "Ruta_al_Cortijo")]
    [InlineData("a/b\\c:d*e?f\"g<h>i|j", "a_b_c_d_e_f_g_h_i_j")]
    [InlineData("  dos   espacios ", "_dos_espacios_")]
    [InlineData("tab\tsalto\nfin", "tab_salto_fin")]
    public void NombreDeFicheroSinCaracteresNoValidos(string input, string expected)
    {
        var name = FileHelper.NormalizeFileName(input);
        Assert.Equal(expected, name);
        Assert.Equal(-1, name.IndexOfAny(Path.GetInvalidFileNameChars()));
    }

    [Theory]
    [InlineData(3.14159, 2, "3.14")]
    [InlineData(2.005, 1, "2.0")]
    [InlineData(-1.5, 0, "-2")]
    [InlineData(7, 3, "7.000")]
    public void NumeroConDecimales(double value, int decimals, string expected)
    {
        var culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        try
        {
            Assert.Equal(expected, FormatingHelper.FloatToStr(value, decimals));
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }

    [Fact]
    public void NumeroUsaLaCulturaDelMovil()
    {
        var culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("es-ES");
        try
        {
            Assert.Equal("1,50", FormatingHelper.FloatToStr(1.5));
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }

    [Fact]
    public void KalmanLaPrimeraLecturaInicializa()
    {
        var k = new KalmanFilter(3);
        k.Process(40, -3, 0.2f, 1000);   // precision por debajo del minimo: cuenta como 1 m

        Assert.Equal((40.0, -3.0, 1000L), (k.Latitude, k.Longitude, k.TimeStamp));
        Assert.Equal(1f, k.Accuracy);
    }

    [Fact]
    public void KalmanPesaMasLaLecturaMasPrecisa()
    {
        var k = new KalmanFilter(3);
        k.SetState(40, -3, 5, 0);

        k.Process(40.001, -3, 50, 0);   // mala, y sin pasar el tiempo
        var afterBad = k.Latitude - 40;
        Assert.InRange(afterBad, 0, 0.0001);

        k.SetState(40, -3, 50, 0);
        k.Process(40.001, -3, 5, 0);    // buena
        Assert.InRange(k.Latitude - 40, 0.0009, 0.001);
    }

    [Fact]
    public void KalmanConElTiempoCreceLaIncertidumbre()
    {
        var quieto = new KalmanFilter(3);
        quieto.SetState(40, -3, 5, 0);
        quieto.Process(40.001, -3, 5, 0);

        var tarde = new KalmanFilter(3);
        tarde.SetState(40, -3, 5, 0);
        tarde.Process(40.001, -3, 5, 60_000);   // un minuto despues: se fia mas de la nueva

        Assert.True(tarde.Latitude > quieto.Latitude);
        Assert.Equal(60_000, tarde.TimeStamp);
        Assert.True(tarde.Accuracy < 5);   // tras medir, la varianza baja

        // Una lectura con hora anterior no mueve el reloj.
        tarde.Process(40.001, -3, 5, 1000);
        Assert.Equal(60_000, tarde.TimeStamp);
    }
}
