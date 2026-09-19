using Hiker.Services;

namespace Hiker.Pages;

/// <summary>
/// Ficha de una ruta guardada: distancia, desnivel positivo y negativo, altitudes, tiempos,
/// velocidades y el perfil de desnivel dibujado. Todo se calcula aqui a partir de los puntos del
/// GPX (lat, lon, altitud, hora); la lista solo tenia la distancia.
/// </summary>
public partial class RouteInfoPage : ContentPage
{
    private readonly string _routeName;
    private readonly RouteService _routeService;
    private readonly TranslationService? _translations;
    private readonly ProfileDrawable _profile = new();

    public RouteInfoPage(string routeName, RouteService routeService, TranslationService? translations)
    {
        InitializeComponent();
        _routeName = routeName;
        _routeService = routeService;
        _translations = translations;
        profileView.Drawable = _profile;
        Title = routeName;
        nameLabel.Text = routeName;
        TranslateUi();
        Loaded += async (_, _) => await LoadAsync();
    }

    private string L(string phrase) => _translations?.Translate(phrase) ?? phrase;

    private void TranslateUi()
    {
        distanceTitle.Text = L("Distancia");
        gainTitle.Text = L("Desnivel positivo");
        lossTitle.Text = L("Desnivel negativo");
        maxAltTitle.Text = L("Altitud máxima");
        minAltTitle.Text = L("Altitud mínima");
        rangeTitle.Text = L("Diferencia de altitud");
        durationTitle.Text = L("Duración");
        movingTitle.Text = L("En movimiento");
        paceTitle.Text = L("Ritmo");
        avgSpeedTitle.Text = L("Velocidad media");
        maxSpeedTitle.Text = L("Velocidad máxima");
        pointsTitle.Text = L("Puntos");
        profileTitle.Text = L("Perfil de desnivel");
        profileHint.Text = L("Altitud (m) según la distancia recorrida (km).");
        showOnMapButton.Text = L("Ver en el mapa");
    }

    private async Task LoadAsync()
    {
        try
        {
            var points = await _routeService.LoadRouteLocationsAsync(_routeName);
            var stats = RouteStats.From(points);
            var ci = System.Globalization.CultureInfo.CurrentCulture;

            dateLabel.Text = stats.Start is { } start
                ? $"{start.ToLocalTime().ToString("f", ci)}"
                : _routeService.GetRouteDate(_routeName).ToString("f", ci);
            distanceValue.Text = $"{stats.DistanceKm.ToString("0.00", ci)} km";
            gainValue.Text = $"+{stats.GainM.ToString("0", ci)} m";
            lossValue.Text = $"−{stats.LossM.ToString("0", ci)} m";
            maxAltValue.Text = stats.HasElevation ? $"{stats.MaxAltM.ToString("0", ci)} m" : "—";
            minAltValue.Text = stats.HasElevation ? $"{stats.MinAltM.ToString("0", ci)} m" : "—";
            rangeValue.Text = stats.HasElevation ? $"{(stats.MaxAltM - stats.MinAltM).ToString("0", ci)} m" : "—";
            durationValue.Text = stats.Duration is { } d ? Fmt(d) : "—";
            movingValue.Text = stats.MovingTime is { } m ? Fmt(m) : "—";
            paceValue.Text = stats.MovingTime is { TotalMinutes: > 0 } mt && stats.DistanceKm > 0.05
                ? $"{Fmt(TimeSpan.FromMinutes(mt.TotalMinutes / stats.DistanceKm))} /km" : "—";
            avgSpeedValue.Text = stats.AvgSpeedKmh > 0 ? $"{stats.AvgSpeedKmh.ToString("0.0", ci)} km/h" : "—";
            maxSpeedValue.Text = stats.MaxSpeedKmh > 0 ? $"{stats.MaxSpeedKmh.ToString("0.0", ci)} km/h" : "—";
            pointsValue.Text = points.Count.ToString(ci);

            _profile.Profile = stats.Profile;
            _profile.Dark = Application.Current?.RequestedTheme == AppTheme.Dark;
            _profile.NoData = L("Esta ruta no lleva altitud.");
            profileView.Invalidate();
        }
        catch (Exception ex)
        {
            await SocShared.ModernDialog.AlertAsync(this, "Error", ex.Message, "OK");
        }
    }

    private static string Fmt(TimeSpan t) => t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}" : $"{t.Minutes}:{t.Seconds:00}";

    private async void OnShowOnMapClicked(object? sender, EventArgs e)
    {
        RouteService.PendingRouteToLoad = _routeName;
        await Shell.Current.GoToAsync("//HomePage");
    }
}

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

/// <summary>El perfil de desnivel: area rellena bajo la linea, con ejes y unas pocas marcas.</summary>
public sealed class ProfileDrawable : IDrawable
{
    public List<(double Km, double Alt)> Profile { get; set; } = [];
    public bool Dark { get; set; }
    public string NoData { get; set; } = string.Empty;

    public void Draw(ICanvas canvas, RectF rect)
    {
        var text = Dark ? Colors.White.WithAlpha(0.8f) : Colors.Black.WithAlpha(0.7f);
        var grid = Dark ? Colors.White.WithAlpha(0.15f) : Colors.Black.WithAlpha(0.12f);
        var accent = Color.FromArgb("#3525CD");
        canvas.FontSize = 11;
        canvas.FontColor = text;
        if (Profile.Count < 2)
        {
            canvas.DrawString(NoData, rect, HorizontalAlignment.Center, VerticalAlignment.Center);
            return;
        }
        const float left = 44, bottom = 22, top = 8, right = 8;
        var plot = new RectF(rect.X + left, rect.Y + top, rect.Width - left - right, rect.Height - top - bottom);
        var minAlt = Profile.Min(p => p.Alt);
        var maxAlt = Profile.Max(p => p.Alt);
        var maxKm = Profile[^1].Km;
        if (maxAlt - minAlt < 10) { maxAlt += 5; minAlt -= 5; }
        // Margen arriba y abajo para que la linea no toque los bordes; redondeo a decenas en el eje.
        var lo = Math.Floor((minAlt - (maxAlt - minAlt) * 0.08) / 10) * 10;
        var hi = Math.Ceiling((maxAlt + (maxAlt - minAlt) * 0.08) / 10) * 10;
        float X(double km) => (float)(plot.X + (maxKm > 0 ? km / maxKm : 0) * plot.Width);
        float Y(double alt) => (float)(plot.Bottom - (alt - lo) / (hi - lo) * plot.Height);

        // Rejilla horizontal: 4 tramos
        canvas.StrokeColor = grid;
        canvas.StrokeSize = 1;
        for (var i = 0; i <= 4; i++)
        {
            var alt = lo + (hi - lo) * i / 4;
            var y = Y(alt);
            canvas.DrawLine(plot.X, y, plot.Right, y);
            canvas.DrawString($"{alt:0}", new RectF(rect.X, y - 7, left - 6, 14), HorizontalAlignment.Right, VerticalAlignment.Center);
        }
        // Eje X: marcas de distancia
        var ticks = maxKm >= 20 ? 5.0 : maxKm >= 8 ? 2.0 : maxKm >= 3 ? 1.0 : 0.5;
        for (var km = 0.0; km <= maxKm + 0.001; km += ticks)
        {
            var x = X(km);
            canvas.DrawLine(x, plot.Bottom, x, plot.Bottom + 4);
            canvas.DrawString(km % 1 == 0 ? $"{km:0}" : $"{km:0.#}", new RectF(x - 20, plot.Bottom + 6, 40, 14), HorizontalAlignment.Center, VerticalAlignment.Top);
        }

        // Area y linea
        var path = new PathF();
        path.MoveTo(X(Profile[0].Km), plot.Bottom);
        foreach (var (km, alt) in Profile)
            path.LineTo(X(km), Y(alt));
        path.LineTo(X(Profile[^1].Km), plot.Bottom);
        path.Close();
        canvas.FillColor = accent.WithAlpha(0.25f);
        canvas.FillPath(path);

        var line = new PathF();
        line.MoveTo(X(Profile[0].Km), Y(Profile[0].Alt));
        foreach (var (km, alt) in Profile.Skip(1))
            line.LineTo(X(km), Y(alt));
        canvas.StrokeColor = accent;
        canvas.StrokeSize = 2;
        canvas.DrawPath(line);
    }
}
