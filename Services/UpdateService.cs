using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hiker.Services;

/// <summary>
/// Comprobacion de version al arrancar (constitucion, seccion 15): consulta un manifiesto en el
/// propio repositorio del proyecto (fuente de confianza) y, si hay una version mas reciente que la
/// instalada, avisa al usuario y le propone actualizar. Es silenciosa y no bloqueante: si no hay red
/// o ya se esta al dia, no molesta.
/// </summary>
public class UpdateService
{
    public const string AppcastUrl = "https://raw.githubusercontent.com/donki/Hiker/main/appcast.json";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(8) };
    private readonly TranslationService _translation;
    private readonly Func<Task<string>> _fetchAppcast;
    private readonly Func<string> _currentVersion;
    private readonly ILinkOpener _links;
    private bool _checkedThisSession;

    public UpdateService(TranslationService translation, ILinkOpener links)
        : this(translation, links, () => Http.GetStringAsync(AppcastUrl), () => AppInfo.Current.VersionString)
    {
    }

    public UpdateService(TranslationService translation, ILinkOpener links,
        Func<Task<string>> fetchAppcast, Func<string> currentVersion)
    {
        _translation = translation;
        _links = links;
        _fetchAppcast = fetchAppcast;
        _currentVersion = currentVersion;
    }

    private string L(string phrase) => _translation.Translate(phrase);

    public async Task CheckAndPromptAsync(IUserDialogs dialogs)
    {
        if (_checkedThisSession)
            return;
        _checkedThisSession = true;

        try
        {
            var manifest = JsonSerializer.Deserialize<Appcast>(await _fetchAppcast());
            if (manifest?.Version is null)
                return;

            var current = _currentVersion();
            if (CompareVersions(manifest.Version, current) <= 0)
                return; // ya se esta en la ultima version (o mas nueva)

            var wantsUpdate = await dialogs.ConfirmAsync(
                L("Actualización disponible"),
                string.Format(L("Hay una versión más reciente ({0}). Tienes la {1}. ¿Quieres actualizar?"), manifest.Version, current),
                L("UpdateButton"), L("Ahora no"));

            if (wantsUpdate && !string.IsNullOrWhiteSpace(manifest.Url))
                await _links.OpenAsync(manifest.Url);
        }
        catch
        {
            // Sin red o manifiesto no disponible: la comprobacion no debe molestar ni bloquear.
        }
    }

    /// <summary>Compara versiones numericas por partes ("2026.07.19.0"). &gt;0 si a es mas nueva que b.</summary>
    public static int CompareVersions(string a, string b)
    {
        var pa = Parts(a);
        var pb = Parts(b);
        var n = Math.Max(pa.Length, pb.Length);
        for (var i = 0; i < n; i++)
        {
            var va = i < pa.Length ? pa[i] : 0;
            var vb = i < pb.Length ? pb[i] : 0;
            if (va != vb)
                return va.CompareTo(vb);
        }
        return 0;
    }

    private static int[] Parts(string v) =>
        v.Split('.').Select(p => int.TryParse(p, out var n) ? n : 0).ToArray();

    private sealed class Appcast
    {
        [JsonPropertyName("version")] public string? Version { get; set; }
        [JsonPropertyName("url")] public string? Url { get; set; }
    }
}
