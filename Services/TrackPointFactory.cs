namespace Hiker.Services;

/// <summary>Lo que el servicio en primer plano saca de cada posicion de Android, ya sin tipos de Android.</summary>
public static class TrackPointFactory
{
    /// <summary>
    /// Punto de la ruta a partir de una lectura del GPS. Lo que la lectura no trae (altitud,
    /// precision, velocidad, rumbo) queda vacio en vez de a cero, salvo la altitud, que el GPX
    /// guarda siempre.
    /// </summary>
    public static Location Create(double latitude, double longitude, double? altitude, double? accuracy,
        double? speed, double? course, long unixTimeMilliseconds) =>
        new(latitude, longitude, altitude ?? 0)
        {
            Accuracy = accuracy,
            Speed = speed,
            Course = course,
            Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(unixTimeMilliseconds),
        };

    /// <summary>Intervalo de lectura elegido en Configuracion, con un minimo de un segundo.</summary>
    public static long IntervalMilliseconds(int? seconds) => Math.Max(1000, (seconds ?? 1) * 1000L);
}
