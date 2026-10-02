using System.Globalization;

namespace Hiker.Services;

/// <summary>Lo que dice la barra de seguimiento: titulo, detalle y si te has salido de la ruta.</summary>
public sealed record FollowStatus(string Title, string Detail, bool OffRoute);

/// <summary>
/// Seguir un recorrido cargado: a que distancia esta el trazado y cuanto queda hasta el final. Se
/// mide contra los vertices de la ruta, que es lo que hay guardado; con la densidad de puntos de un
/// GPX es suficiente y no hace falta proyectar sobre cada segmento.
/// </summary>
public sealed class RouteFollower
{
    /// <summary>Distancia al trazado a partir de la cual se avisa de que te has salido.</summary>
    public const double OffRouteMeters = 50;

    private readonly Func<string, string> _translate;
    private List<Location> _route = [];

    /// <summary>Metros acumulados desde el inicio hasta cada punto: evita recorrer la ruta entera
    /// en cada actualizacion de GPS para saber cuanto queda.</summary>
    private double[] _cumulative = [];

    public RouteFollower(Func<string, string> translate) => _translate = translate;

    public bool IsFollowing => _route.Count > 0;

    public double TotalMeters => _cumulative.Length == 0 ? 0 : _cumulative[^1];

    /// <summary>Empieza a seguir una ruta y devuelve el primer estado de la barra.</summary>
    public FollowStatus Start(List<Location> points)
    {
        _route = points;
        _cumulative = new double[points.Count];
        for (var i = 1; i < points.Count; i++)
            _cumulative[i] = _cumulative[i - 1] + MetersBetween(points[i - 1], points[i]);

        return new FollowStatus(_translate("Siguiendo la ruta"),
            string.Format(_translate("Longitud total: {0}"), FormatDistance(TotalMeters)), false);
    }

    public void Stop()
    {
        _route = [];
        _cumulative = [];
    }

    /// <summary>Estado de la barra para la posicion actual; null si no se sigue ninguna ruta.</summary>
    public FollowStatus? Update(Location current)
    {
        if (_route.Count == 0)
            return null;

        var nearest = 0;
        var nearestMeters = double.MaxValue;
        for (var i = 0; i < _route.Count; i++)
        {
            var d = MetersBetween(current, _route[i]);
            if (d < nearestMeters)
            {
                nearestMeters = d;
                nearest = i;
            }
        }

        var remaining = TotalMeters - _cumulative[nearest];
        var offRoute = nearestMeters > OffRouteMeters;

        var title = offRoute
            ? string.Format(_translate("Te has salido: a {0} de la ruta"), FormatDistance(nearestMeters))
            : string.Format(_translate("En la ruta: a {0} del trazado"), FormatDistance(nearestMeters));

        // Lo que "queda" solo significa algo si estas sobre la ruta: fuera de ella el punto mas
        // cercano puede ser el final y saldria "quedan 0 m" estando a kilometros.
        var detail = offRoute
            ? string.Format(_translate("Longitud total: {0}"), FormatDistance(TotalMeters))
            : string.Format(_translate("Quedan {0}"), FormatDistance(remaining));

        return new FollowStatus(title, detail, offRoute);
    }

    public static double MetersBetween(Location a, Location b) =>
        Location.CalculateDistance(a, b, DistanceUnits.Kilometers) * 1000;

    /// <summary>«850 m» por debajo del kilometro y «1,2 km» por encima, en la cultura actual.</summary>
    public static string FormatDistance(double meters)
    {
        var ci = CultureInfo.CurrentCulture;
        return meters >= 1000
            ? $"{(meters / 1000).ToString("0.0", ci)} km"
            : $"{Math.Round(meters).ToString(ci)} m";
    }
}
