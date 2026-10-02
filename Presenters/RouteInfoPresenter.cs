using System.ComponentModel;
using System.Globalization;
using Hiker.Services;

namespace Hiker.Presenters;

/// <summary>
/// La ficha de una ruta guardada: distancia, desnivel positivo y negativo, altitudes, tiempos,
/// velocidades y el perfil de desnivel. Todo se calcula a partir de los puntos del GPX (lat, lon,
/// altitud, hora). La pagina enlaza sus etiquetas a estas propiedades.
/// </summary>
public sealed class RouteInfoPresenter : INotifyPropertyChanged
{
    private const string None = "—";

    private readonly RouteService _routes;
    private readonly Func<string, string> _translate;
    private readonly IUserDialogs _dialogs;
    private readonly INavigator _navigator;

    public RouteInfoPresenter(string routeName, RouteService routes, Func<string, string> translate,
        IUserDialogs dialogs, INavigator navigator)
    {
        Name = routeName;
        _routes = routes;
        _translate = translate;
        _dialogs = dialogs;
        _navigator = navigator;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private string L(string phrase) => _translate(phrase);

    public string Name { get; }

    // Rotulos
    public string DistanceTitle => L("Distancia");
    public string GainTitle => L("Desnivel positivo");
    public string LossTitle => L("Desnivel negativo");
    public string MaxAltTitle => L("Altitud máxima");
    public string MinAltTitle => L("Altitud mínima");
    public string RangeTitle => L("Diferencia de altitud");
    public string DurationTitle => L("Duración");
    public string MovingTitle => L("En movimiento");
    public string PaceTitle => L("Ritmo");
    public string AvgSpeedTitle => L("Velocidad media");
    public string MaxSpeedTitle => L("Velocidad máxima");
    public string PointsTitle => L("Puntos");
    public string ProfileTitle => L("Perfil de desnivel");
    public string ProfileHint => L("Altitud (m) según la distancia recorrida (km).");
    public string ShowOnMap => L("Ver en el mapa");

    // Valores (vacios hasta que se carga la ruta)
    public string Date { get; private set; } = "";
    public string DistanceValue { get; private set; } = "";
    public string GainValue { get; private set; } = "";
    public string LossValue { get; private set; } = "";
    public string MaxAltValue { get; private set; } = "";
    public string MinAltValue { get; private set; } = "";
    public string RangeValue { get; private set; } = "";
    public string DurationValue { get; private set; } = "";
    public string MovingValue { get; private set; } = "";
    public string PaceValue { get; private set; } = "";
    public string AvgSpeedValue { get; private set; } = "";
    public string MaxSpeedValue { get; private set; } = "";
    public string PointsValue { get; private set; } = "";

    /// <summary>El dibujo del perfil (lo pinta un GraphicsView).</summary>
    public ProfileDrawable Profile { get; } = new();

    /// <summary>Carga la ruta y rellena la ficha. Devuelve false si fallo (y ya se aviso).</summary>
    public async Task<bool> LoadAsync(bool dark)
    {
        try
        {
            var points = await _routes.LoadRouteLocationsAsync(Name);
            var s = RouteStats.From(points);
            var ci = CultureInfo.CurrentCulture;

            Date = s.Start is { } start
                ? start.ToLocalTime().ToString("f", ci)
                : _routes.GetRouteDate(Name).ToString("f", ci);
            DistanceValue = $"{s.DistanceKm.ToString("0.00", ci)} km";
            GainValue = $"+{s.GainM.ToString("0", ci)} m";
            LossValue = $"−{s.LossM.ToString("0", ci)} m";
            MaxAltValue = s.HasElevation ? $"{s.MaxAltM.ToString("0", ci)} m" : None;
            MinAltValue = s.HasElevation ? $"{s.MinAltM.ToString("0", ci)} m" : None;
            RangeValue = s.HasElevation ? $"{(s.MaxAltM - s.MinAltM).ToString("0", ci)} m" : None;
            DurationValue = s.Duration is { } d ? FormatTime(d) : None;
            MovingValue = s.MovingTime is { } m ? FormatTime(m) : None;
            PaceValue = s.MovingTime is { TotalMinutes: > 0 } mt && s.DistanceKm > 0.05
                ? $"{FormatTime(TimeSpan.FromMinutes(mt.TotalMinutes / s.DistanceKm))} /km" : None;
            AvgSpeedValue = s.AvgSpeedKmh > 0 ? $"{s.AvgSpeedKmh.ToString("0.0", ci)} km/h" : None;
            MaxSpeedValue = s.MaxSpeedKmh > 0 ? $"{s.MaxSpeedKmh.ToString("0.0", ci)} km/h" : None;
            PointsValue = points.Count.ToString(ci);

            Profile.Profile = s.Profile;
            Profile.Dark = dark;
            Profile.NoData = L("Esta ruta no lleva altitud.");

            // Cadena vacia = han cambiado todas: la pagina repinta todas las etiquetas.
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
            return true;
        }
        catch (Exception ex)
        {
            await _dialogs.AlertAsync(L("Error"), ex.Message, "OK");
            return false;
        }
    }

    /// <summary>«1:05:09» con horas; «5:09» sin ellas.</summary>
    public static string FormatTime(TimeSpan t) =>
        t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}" : $"{t.Minutes}:{t.Seconds:00}";

    /// <summary>Ver en el mapa: se marca la ruta y se salta al mapa, que la carga al aparecer.</summary>
    public Task ShowOnMapAsync()
    {
        RouteService.PendingRouteToLoad = Name;
        return _navigator.GoToAsync("//HomePage");
    }
}
