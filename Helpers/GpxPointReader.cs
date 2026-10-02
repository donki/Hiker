using System.Globalization;
using System.Xml.Linq;

namespace Hiker.Helpers;

/// <summary>
/// Saca los puntos de un GPX importado sin pasar por el serializador estricto, que solo entendía
/// tracks (<c>trkpt</c>) del espacio de nombres de GPX 1.1 y obligaba a que cada punto trajera
/// <c>&lt;time&gt;</c>. Con esas tres condiciones fallaba la mayoría de ficheros descargados: un
/// GPX 1.0 usa otro espacio de nombres; las rutas (<c>rtept</c>) y los puntos sueltos (<c>wpt</c>)
/// se ignoraban; y sin <c>&lt;time&gt;</c> la fecha quedaba en <c>DateTime.MinValue</c> y reventaba
/// al aplicarle el desfase horario. Aquí se busca por nombre local (cualquier espacio de nombres) y
/// la marca de tiempo es opcional: para pintar la ruta solo hacen falta latitud y longitud.
/// </summary>
public static class GpxPointReader
{
    public static List<Location> Read(string gpxContent)
    {
        var points = new List<Location>();
        var doc = XDocument.Parse(gpxContent);

        List<XElement> ByName(string name) =>
            doc.Descendants().Where(e => e.Name.LocalName == name).ToList();

        // Preferencia: track grabado > ruta planificada > puntos sueltos.
        var nodes = ByName("trkpt");
        if (nodes.Count == 0) nodes = ByName("rtept");
        if (nodes.Count == 0) nodes = ByName("wpt");

        foreach (var node in nodes)
        {
            if (!TryParseCoordinate(node.Attribute("lat")?.Value, out var latitude) ||
                !TryParseCoordinate(node.Attribute("lon")?.Value, out var longitude))
                continue;

            var location = new Location(latitude, longitude);
            var child = node.Elements().ToList();

            if (TryParseCoordinate(child.FirstOrDefault(e => e.Name.LocalName == "ele")?.Value, out var elevation))
                location.Altitude = elevation;

            var time = child.FirstOrDefault(e => e.Name.LocalName == "time")?.Value;
            if (DateTimeOffset.TryParse(time, CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var timestamp))
                location.Timestamp = timestamp;

            points.Add(location);
        }

        return points;
    }

    private static bool TryParseCoordinate(string? value, out double result) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
}
