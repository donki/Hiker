using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Hiker.Services;

/// <summary>Resultado de ajustar una ruta: los puntos nuevos y que se ha tocado.</summary>
public sealed record MatchResult(
    List<Location> Points,
    int SnappedToPath,
    int MovedOutOfBuilding)
{
    public int Changed => SnappedToPath + MovedOutOfBuilding;

    public bool AnyChange => Changed > 0;
}

/// <summary>
/// Ajusta una ruta grabada contra el mapa: la pega a los caminos y la saca de los edificios.
/// </summary>
/// <remarks>
/// <para><b>El problema.</b> El GPS de un movil se equivoca varios metros, y mas entre edificios
/// altos o bajo arboles. El resultado es una linea que serpentea y que a veces atraviesa una casa,
/// cosa que evidentemente no ocurrio.</para>
///
/// <para><b>Lo que NO se hace, y es lo importante.</b> Esto es una aplicacion de senderismo. Andar
/// fuera de camino es normal y legitimo, asi que <b>no</b> se fuerza cada punto al camino mas
/// cercano: eso convertiria una travesia campo a traves en un paseo por carretera y seria
/// falsificar el recorrido. Solo se pega un punto cuando hay un camino <i>muy</i> cerca
/// —<see cref="SnapRadiusMeters"/> metros—, que es el margen de error tipico del GPS. Mas lejos se
/// respeta lo grabado.</para>
///
/// <para><b>Los edificios son otra cosa.</b> Un punto dentro de un edificio es error seguro: no se
/// atraviesa una pared. Esos se sacan siempre al borde mas cercano, haya camino o no.</para>
///
/// <para><b>De donde salen los datos.</b> De OpenStreetMap por Overpass, la misma fuente que las
/// teselas del mapa. Es un servicio publico con normas de uso razonable: se pide <b>una sola vez</b>
/// el rectangulo de toda la ruta en lugar de consultar punto por punto.</para>
/// </remarks>
public sealed class MapMatchService
{
    /// <summary>Distancia maxima para dar por bueno que el punto pertenece a ese camino.</summary>
    private const double SnapRadiusMeters = 20;

    /// <summary>Margen alrededor de la ruta al pedir el mapa, para no quedarnos cortos en los bordes.</summary>
    private const double BboxMarginDegrees = 0.002;

    private static readonly string[] Endpoints =
    [
        "https://overpass-api.de/api/interpreter",
        "https://overpass.kumi.systems/api/interpreter",
    ];

    private readonly HttpClient _http;

    public MapMatchService(HttpClient http) => _http = http;

    /// <summary>
    /// Ajusta la ruta. Devuelve <c>null</c> si no se pudo consultar el mapa, para poder distinguir
    /// "no habia nada que corregir" de "no se ha podido comprobar".
    /// </summary>
    public async Task<MatchResult?> MatchAsync(IReadOnlyList<Location> track,
        CancellationToken cancellationToken = default)
    {
        if (track.Count < 2)
        {
            return new MatchResult([.. track], 0, 0);
        }

        var map = await FetchAsync(track, cancellationToken).ConfigureAwait(false);
        if (map is null)
        {
            return null;
        }

        var (ways, buildings) = map.Value;

        var result = new List<Location>(track.Count);
        var snapped = 0;
        var moved = 0;

        foreach (var point in track)
        {
            var adjusted = point;
            var wasSnapped = false;

            // 1. Pegar al camino, pero solo si esta al alcance del error del GPS.
            if (NearestOnWays(point, ways) is { } onPath &&
                Distance(point, onPath) <= SnapRadiusMeters)
            {
                adjusted = onPath;
                wasSnapped = true;
            }

            // 2. Sacar de dentro de un edificio. Va despues a proposito: pegar al camino puede
            //    haber resuelto ya el problema, y si no, esto lo arregla igualmente.
            if (InsideAny(adjusted, buildings) is { } outline)
            {
                adjusted = outline;

                // Cada punto cuenta una sola vez, y manda el motivo mas grave: haber estado dentro
                // de un edificio. Si no, un mismo punto sumaria en las dos cuentas y el resumen
                // diria que se han tocado mas puntos de los que hay.
                moved++;
            }
            else if (wasSnapped)
            {
                snapped++;
            }

            result.Add(new Location(adjusted.Latitude, adjusted.Longitude)
            {
                Timestamp = point.Timestamp,
                Altitude = point.Altitude,
                Accuracy = point.Accuracy,
                Speed = point.Speed,
            });
        }

        return new MatchResult(result, snapped, moved);
    }

    // =======================================================================
    // Mapa
    // =======================================================================

    private async Task<(List<List<Location>> Ways, List<List<Location>> Buildings)?> FetchAsync(
        IReadOnlyList<Location> track, CancellationToken cancellationToken)
    {
        var south = track.Min(p => p.Latitude) - BboxMarginDegrees;
        var north = track.Max(p => p.Latitude) + BboxMarginDegrees;
        var west = track.Min(p => p.Longitude) - BboxMarginDegrees;
        var east = track.Max(p => p.Longitude) + BboxMarginDegrees;

        var bbox = string.Create(CultureInfo.InvariantCulture, $"{south:F6},{west:F6},{north:F6},{east:F6}");

        // Solo por donde se puede andar. Se dejan fuera autopistas y vias rapidas: si el GPS pone
        // un punto ahi, pegarlo seria empeorarlo.
        var query =
            "[out:json][timeout:25];(" +
            $"way[\"highway\"~\"^(footway|path|track|pedestrian|steps|cycleway|bridleway|living_street|residential|service|unclassified|tertiary|secondary)$\"]({bbox});" +
            $"way[\"building\"]({bbox});" +
            ");out geom;";

        foreach (var endpoint in Endpoints)
        {
            try
            {
                using var content = new StringContent(query, Encoding.UTF8, "text/plain");
                using var response = await _http.PostAsync(endpoint, content, cancellationToken)
                    .ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    continue;   // Overpass devuelve 429 cuando esta saturado: se prueba el otro.
                }

                await using var stream = await response.Content
                    .ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

                return Parse(await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
                    .ConfigureAwait(false));
            }
            catch (Exception)
            {
                // Sin red, tiempo agotado o respuesta rara: se intenta el siguiente servidor.
            }
        }

        return null;
    }

    private static (List<List<Location>>, List<List<Location>>) Parse(JsonDocument document)
    {
        var ways = new List<List<Location>>();
        var buildings = new List<List<Location>>();

        if (!document.RootElement.TryGetProperty("elements", out var elements))
        {
            return (ways, buildings);
        }

        foreach (var element in elements.EnumerateArray())
        {
            if (!element.TryGetProperty("geometry", out var geometry))
            {
                continue;
            }

            var line = new List<Location>();
            foreach (var node in geometry.EnumerateArray())
            {
                if (node.TryGetProperty("lat", out var lat) && node.TryGetProperty("lon", out var lon))
                {
                    line.Add(new Location(lat.GetDouble(), lon.GetDouble()));
                }
            }

            if (line.Count < 2)
            {
                continue;
            }

            var isBuilding = element.TryGetProperty("tags", out var tags) &&
                             tags.TryGetProperty("building", out _);

            (isBuilding ? buildings : ways).Add(line);
        }

        return (ways, buildings);
    }

    // =======================================================================
    // Geometria
    // =======================================================================

    /// <summary>Punto mas cercano de cualquier camino, o <c>null</c> si no hay ninguno.</summary>
    private static Location? NearestOnWays(Location point, List<List<Location>> ways)
    {
        Location? best = null;
        var bestDistance = double.MaxValue;

        foreach (var way in ways)
        {
            for (var i = 0; i < way.Count - 1; i++)
            {
                var candidate = ProjectOnSegment(point, way[i], way[i + 1]);
                var distance = Distance(point, candidate);

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                }
            }
        }

        return best;
    }

    /// <summary>
    /// Si el punto cae dentro de algun edificio, devuelve el punto mas cercano de su contorno; si
    /// no, <c>null</c>.
    /// </summary>
    private static Location? InsideAny(Location point, List<List<Location>> buildings)
    {
        foreach (var building in buildings)
        {
            if (!IsInside(point, building))
            {
                continue;
            }

            Location? best = null;
            var bestDistance = double.MaxValue;

            for (var i = 0; i < building.Count - 1; i++)
            {
                var candidate = ProjectOnSegment(point, building[i], building[i + 1]);
                var distance = Distance(point, candidate);

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                }
            }

            if (best is not null)
            {
                return Nudge(best, point);
            }
        }

        return null;
    }

    /// <summary>
    /// Aparta el punto un metro hacia fuera del edificio.
    /// </summary>
    /// <remarks>
    /// Dejarlo justo en la pared lo deja en el limite, y cualquier redondeo posterior puede
    /// devolverlo dentro. Un metro no cambia la ruta y quita el problema.
    /// </remarks>
    private static Location Nudge(Location onEdge, Location from)
    {
        var dLat = onEdge.Latitude - from.Latitude;
        var dLon = onEdge.Longitude - from.Longitude;

        var length = Math.Sqrt((dLat * dLat) + (dLon * dLon));
        if (length < 1e-12)
        {
            return onEdge;
        }

        // Un metro en grados de latitud; en longitud se corrige por el coseno de la latitud, que es
        // lo que hace que un grado valga menos cuanto mas al norte se esta.
        const double MeterInDegrees = 1.0 / 111_320.0;
        var cos = Math.Max(Math.Cos(onEdge.Latitude * Math.PI / 180), 1e-6);

        return new Location(
            onEdge.Latitude + (dLat / length * MeterInDegrees),
            onEdge.Longitude + (dLon / length * MeterInDegrees / cos));
    }

    /// <summary>Proyeccion perpendicular del punto sobre el segmento, sin salirse de sus extremos.</summary>
    private static Location ProjectOnSegment(Location point, Location a, Location b)
    {
        // Se trabaja en grados corrigiendo la longitud por la latitud. A la escala de una ruta
        // —unos pocos kilometros— la Tierra es plana de sobra para esto.
        var cos = Math.Max(Math.Cos(point.Latitude * Math.PI / 180), 1e-6);

        var ax = a.Longitude * cos;
        var ay = a.Latitude;
        var bx = b.Longitude * cos;
        var by = b.Latitude;
        var px = point.Longitude * cos;
        var py = point.Latitude;

        var dx = bx - ax;
        var dy = by - ay;
        var lengthSquared = (dx * dx) + (dy * dy);

        if (lengthSquared < 1e-18)
        {
            return a;   // Segmento degenerado: los dos extremos son el mismo punto.
        }

        var t = Math.Clamp((((px - ax) * dx) + ((py - ay) * dy)) / lengthSquared, 0, 1);

        return new Location(ay + (t * dy), (ax + (t * dx)) / cos);
    }

    /// <summary>Punto dentro de un poligono, por el metodo del numero de cruces.</summary>
    private static bool IsInside(Location point, List<Location> polygon)
    {
        var inside = false;

        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            var yi = polygon[i].Latitude;
            var xi = polygon[i].Longitude;
            var yj = polygon[j].Latitude;
            var xj = polygon[j].Longitude;

            if (yi > point.Latitude != yj > point.Latitude &&
                point.Longitude < ((xj - xi) * (point.Latitude - yi) / (yj - yi)) + xi)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    /// <summary>Distancia en metros entre dos puntos (haversine).</summary>
    private static double Distance(Location a, Location b)
    {
        const double EarthRadiusMeters = 6_371_000;

        var lat1 = a.Latitude * Math.PI / 180;
        var lat2 = b.Latitude * Math.PI / 180;
        var dLat = lat2 - lat1;
        var dLon = (b.Longitude - a.Longitude) * Math.PI / 180;

        var h = (Math.Sin(dLat / 2) * Math.Sin(dLat / 2)) +
                (Math.Cos(lat1) * Math.Cos(lat2) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2));

        return 2 * EarthRadiusMeters * Math.Asin(Math.Min(1, Math.Sqrt(h)));
    }
}
