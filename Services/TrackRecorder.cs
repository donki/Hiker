using System.Globalization;

namespace Hiker.Services;

/// <summary>
/// La grabacion de una ruta, viva por su cuenta y no dentro de una pagina.
/// </summary>
/// <remarks>
/// Antes la grabacion vivia en <c>HomePage</c>: los puntos se guardaban en una lista de la pagina y
/// solo se escribian en disco al pulsar «guardar». Con la pantalla apagada o la app en segundo
/// plano eso se caia por dos sitios a la vez — la pagina deja de recibir posiciones y, si Android
/// mata el proceso durante una ruta larga, se pierde todo lo grabado hasta ese momento.
///
/// Aqui la grabacion es un servicio de aplicacion, quien entrega los puntos es el servicio en
/// primer plano (que si sigue vivo con la pantalla apagada), y **cada punto se escribe en el diario
/// nada mas llegar**: si el sistema mata el proceso a mitad de ruta, al volver a abrir la
/// aplicacion los puntos siguen ahi.
/// </remarks>
public sealed class TrackRecorder
{
    /// <summary>Diario de la grabacion en curso: una linea por punto, escrita al vuelo.</summary>
    private const string JournalFileName = "recording.csv";

    private readonly GpsFilterService _filter;
    private readonly List<Location> _points = [];
    private readonly Lock _gate = new();

    public TrackRecorder(GpsFilterService filter) => _filter = filter;

    /// <summary>Se ha añadido un punto a la ruta. Lo usa el mapa para ir pintando la traza.</summary>
    public event EventHandler<Location>? PointAdded;

    /// <summary>Ha empezado o parado la grabacion, venga la orden de donde venga.</summary>
    public event EventHandler? RecordingChanged;

    public bool IsRecording { get; private set; }

    public DateTime StartedAt { get; private set; }

    public double DistanceKm { get; private set; }

    public int PointCount
    {
        get
        {
            lock (_gate)
                return _points.Count;
        }
    }

    public List<Location> Snapshot()
    {
        lock (_gate)
            return [.. _points];
    }

    private static string JournalPath => Path.Combine(FileSystem.AppDataDirectory, JournalFileName);

    // ==================================================================================
    //  Ciclo de la grabacion
    // ==================================================================================

    public void Start()
    {
        lock (_gate)
        {
            _points.Clear();
            DistanceKm = 0;
            StartedAt = DateTime.Now;
            IsRecording = true;

            // Se reinicia el filtro para no arrastrar el estado del Kalman de una grabacion
            // anterior, que sesgaba los primeros puntos de la nueva ruta.
            _filter.Reset();

            TryDeleteJournal();
        }

        RecordingChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Stop()
    {
        lock (_gate)
            IsRecording = false;

        RecordingChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Tira la ruta grabada y borra el diario. Es lo que hace «descartar».</summary>
    public void Discard()
    {
        lock (_gate)
        {
            _points.Clear();
            DistanceKm = 0;
            TryDeleteJournal();
        }

        RecordingChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Entra un punto del GPS. Devuelve true si ha pasado el filtro y se ha guardado. Lo llama el
    /// servicio en primer plano, que es quien escucha al GPS mientras dura la grabacion.
    /// </summary>
    public bool Push(Location raw)
    {
        if (!IsRecording)
            return false;

        var point = _filter.ProcessLocation(raw);
        if (point is null)
            return false;

        lock (_gate)
        {
            // La distancia se acumula tramo a tramo sobre los puntos ya filtrados: sumar los crudos
            // infla el total con el temblor del GPS estando parado.
            if (_points.Count > 0)
                DistanceKm += Location.CalculateDistance(_points[^1], point, DistanceUnits.Kilometers);

            _points.Add(point);
            AppendToJournal(point);
        }

        PointAdded?.Invoke(this, point);
        return true;
    }

    // ==================================================================================
    //  Diario en disco: lo que salva la ruta si el sistema mata el proceso
    // ==================================================================================

    private static void AppendToJournal(Location point)
    {
        try
        {
            var line = string.Create(CultureInfo.InvariantCulture,
                $"{point.Timestamp.UtcTicks},{point.Latitude},{point.Longitude},{point.Altitude ?? 0},{point.Accuracy ?? 0}");

            // Append + flush en cada punto: un fichero abierto y sin volcar no sobrevive a que el
            // sistema mate el proceso, que es justo el caso del que hay que protegerse.
            File.AppendAllText(JournalPath, line + Environment.NewLine);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Si no se puede escribir el diario, la grabacion en memoria sigue: se pierde la red de
            // seguridad, no la ruta.
            System.Diagnostics.Debug.WriteLine($"No se pudo escribir el diario de grabacion: {ex.Message}");
        }
    }

    private static void TryDeleteJournal()
    {
        try
        {
            if (File.Exists(JournalPath))
                File.Delete(JournalPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Debug.WriteLine($"No se pudo borrar el diario de grabacion: {ex.Message}");
        }
    }

    /// <summary>
    /// Puntos de una grabacion que se quedo a medias (el sistema mato el proceso). Vacio si no hay
    /// nada pendiente. No borra el diario: eso lo decide el usuario al recuperar o descartar.
    /// </summary>
    public static List<Location> ReadPendingJournal()
    {
        var points = new List<Location>();

        try
        {
            if (!File.Exists(JournalPath))
                return points;

            foreach (var line in File.ReadAllLines(JournalPath))
            {
                var parts = line.Split(',');
                if (parts.Length < 3)
                    continue;

                if (!long.TryParse(parts[0], out var ticks) ||
                    !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var lat) ||
                    !double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var lon))
                    continue;

                double.TryParse(parts.ElementAtOrDefault(3), NumberStyles.Float, CultureInfo.InvariantCulture, out var alt);
                double.TryParse(parts.ElementAtOrDefault(4), NumberStyles.Float, CultureInfo.InvariantCulture, out var accuracy);

                points.Add(new Location(lat, lon, alt)
                {
                    Timestamp = new DateTimeOffset(ticks, TimeSpan.Zero),
                    Accuracy = accuracy > 0 ? accuracy : null,
                });
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException)
        {
            System.Diagnostics.Debug.WriteLine($"No se pudo leer el diario de grabacion: {ex.Message}");
        }

        return points;
    }

    /// <summary>
    /// Reanuda una grabacion que se corto porque Android mato el proceso. Lo llama el servicio en
    /// primer plano al recrearse: si hay diario, es que se estaba grabando, asi que se cargan los
    /// puntos y se sigue por donde iba en vez de empezar una ruta nueva y perder la anterior.
    /// </summary>
    public bool ResumeIfInterrupted()
    {
        if (IsRecording)
            return false;

        var pending = ReadPendingJournal();
        if (pending.Count == 0)
            return false;

        Adopt(pending);

        lock (_gate)
            IsRecording = true;

        RecordingChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>Recupera en memoria una grabacion interrumpida, para poder guardarla.</summary>
    public void Adopt(List<Location> points)
    {
        lock (_gate)
        {
            _points.Clear();
            _points.AddRange(points);

            DistanceKm = 0;
            for (var i = 1; i < _points.Count; i++)
                DistanceKm += Location.CalculateDistance(_points[i - 1], _points[i], DistanceUnits.Kilometers);

            StartedAt = _points.Count > 0 ? _points[0].Timestamp.LocalDateTime : DateTime.Now;
        }

        RecordingChanged?.Invoke(this, EventArgs.Empty);
    }

    public static void DeletePendingJournal() => TryDeleteJournal();
}
