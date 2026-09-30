// Sustitutos de FileSystem y Preferences de MAUI para los ficheros de la app enlazados aqui. Al vivir
// en el espacio Hiker.Services, el compilador los elige antes que los de Microsoft.Maui.Storage (cuya
// version portable no funciona fuera de un movil).
namespace Hiker.Services;

internal static class FileSystem
{
    public static string AppDataDirectory { get; set; } =
        Path.Combine(Path.GetTempPath(), "hiker-tests-" + Environment.ProcessId);
}

internal static class Preferences
{
    public static Dictionary<string, string> Values { get; } = [];

    public static string Get(string key, string defaultValue) =>
        Values.TryGetValue(key, out var v) ? v : defaultValue;

    public static void Set(string key, string value) => Values[key] = value;
}
