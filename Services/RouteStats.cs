namespace Hiker.Services;

/// <summary>Lo que se saca de los puntos de una ruta. Los desniveles se suavizan para no sumar el ruido del GPS.</summary>
public sealed class RouteStats
{
    public double DistanceKm;
    public double GainM, LossM, MaxAltM = double.MinValue, MinAltM = double.MaxValue;
    public bool HasElevation;
    public DateTimeOffset? Start, End;
    public TimeSpan? Duration, MovingTime;
    public double AvgSpeedKmh, MaxSpeedKmh;
    /// <summary>(distancia km, altitud m) por punto, para el perfil.</summary>
    public List<(double Km, double Alt)> Profile = [];

    public static RouteStats From(List<Location> points)
    {
        var s = new RouteStats();
        if (points.Count == 0)
            return s;
        // Umbral de desnivel: solo cuenta un cambio cuando la altitud se aleja mas de 3 m del ultimo
        // punto «anclado». Sin esto, el vaiven de ±1 m del GPS sumaba cientos de metros ficticios.
        const double threshold = 3.0;
        double? anchor = null;
        double moving = 0;
        var distance = 0.0;
        var timed = points.Where(p => p.Timestamp != default).ToList();
        s.Start = timed.Count > 0 ? timed.Min(p => p.Timestamp) : null;
        s.End = timed.Count > 0 ? timed.Max(p => p.Timestamp) : null;
        for (var i = 0; i < points.Count; i++)
        {
            var p = points[i];
            if (i > 0)
            {
                var prev = points[i - 1];
                var d = Location.CalculateDistance(prev, p, DistanceUnits.Kilometers);
                distance += d;
                if (prev.Timestamp != default && p.Timestamp != default)
                {
                    var dt = (p.Timestamp - prev.Timestamp).TotalHours;
                    if (dt > 0)
                    {
                        var v = d / dt;
                        // Tramos por debajo de 0,5 km/h son paradas; por encima de 40 km/h, saltos del GPS.
                        if (v >= 0.5 && v <= 40)
                        {
                            moving += dt;
                            if (v > s.MaxSpeedKmh) s.MaxSpeedKmh = v;
                        }
                    }
                }
            }
            if (p.Altitude is { } alt && alt != 0)
            {
                s.HasElevation = true;
                if (alt > s.MaxAltM) s.MaxAltM = alt;
                if (alt < s.MinAltM) s.MinAltM = alt;
                if (anchor is null)
                    anchor = alt;
                else if (Math.Abs(alt - anchor.Value) >= threshold)
                {
                    if (alt > anchor.Value) s.GainM += alt - anchor.Value; else s.LossM += anchor.Value - alt;
                    anchor = alt;
                }
                s.Profile.Add((distance, alt));
            }
        }
        s.DistanceKm = distance;
        if (s.Start is not null && s.End is not null)
            s.Duration = s.End - s.Start;
        if (moving > 0)
        {
            s.MovingTime = TimeSpan.FromHours(moving);
            s.AvgSpeedKmh = distance / moving;
        }
        return s;
    }
}
