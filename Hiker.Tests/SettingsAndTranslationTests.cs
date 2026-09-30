using System.Globalization;
using System.Text.RegularExpressions;
using Hiker.Services;

namespace Hiker.Tests;

/// <summary>Ajustes (guardar, leer, valores malos) y traducciones es/en del CSV.</summary>
public partial class SettingsAndTranslationTests : IDisposable
{
    public SettingsAndTranslationTests() => Preferences.Values.Clear();

    public void Dispose() => Preferences.Values.Clear();

    public static TheoryData<string, string, string> Values => new()
    {
        { "Language", "en", "en" },
        { "MaxSpeed", "7", "7" },
        { "MinAccuracy", "4", "4" },
        { "TimerInterval", "5", "5" },
        { "KalmanFilterEnabled", "False", "False" },
        { "AverageFilterEnabled", "False", "False" },
        { "SpeedFilterEnabled", "True", "True" },
        { "AccuracyFilterEnabled", "False", "False" },
        { "Accuracy", "30", "30" },
        { "WindowSize", "6", "6" },
        { "UseNativeGeolocation", "False", "False" },
        { "ShowlocalizationData", "False", "False" },
    };

    [Theory]
    [MemberData(nameof(Values))]
    public void CadaAjusteSeGuardaYSeLee(string key, string value, string expected)
    {
        var settings = new SettingsService();
        settings.SaveSetting(key, value);
        Assert.Equal(expected, settings.GetSetting(key));
        Assert.Contains($"\"{key}\":", Preferences.Values["AppSettings"]);   // se guarda solo
    }

    [Theory]
    [InlineData("MaxSpeed")]
    [InlineData("MinAccuracy")]
    [InlineData("TimerInterval")]
    [InlineData("KalmanFilterEnabled")]
    [InlineData("AverageFilterEnabled")]
    [InlineData("SpeedFilterEnabled")]
    [InlineData("AccuracyFilterEnabled")]
    [InlineData("Accuracy")]
    [InlineData("WindowSize")]
    [InlineData("UseNativeGeolocation")]
    [InlineData("ShowlocalizationData")]
    public void UnValorQueNoSeEntiendeNoCambiaNada(string key)
    {
        var settings = new SettingsService();
        var before = settings.GetSetting(key);
        settings.SaveSetting(key, "basura");
        Assert.Equal(before, settings.GetSetting(key));
    }

    [Fact]
    public void AjusteDesconocido()
    {
        var settings = new SettingsService();
        Assert.Null(settings.GetSetting("NoExiste"));
        settings.SaveSetting("NoExiste", "1");   // no lanza
    }

    [Fact]
    public async Task LosAjustesSobrevivenAlReinicioYSePuedenRestablecer()
    {
        var first = new SettingsService();
        first.AppSettings.MaxSpeed = 9;
        first.AppSettings.WindowSize = 7;
        await first.SaveSettingsAsync();

        var second = new SettingsService();
        await second.LoadSettingsAsync();
        Assert.Equal((9.0, 7), (second.AppSettings.MaxSpeed, second.AppSettings.WindowSize));

        await second.ResetSettingsAsync();
        Assert.Equal((3.0, 3, false), (second.AppSettings.MaxSpeed, second.AppSettings.WindowSize, second.AppSettings.SpeedFilterEnabled));
        var third = new SettingsService();
        await third.LoadSettingsAsync();
        Assert.Equal(3.0, third.AppSettings.MaxSpeed);
    }

    [Fact]
    public async Task SinAjustesGuardadosQuedanLosDeFabrica()
    {
        var settings = new SettingsService();
        await settings.LoadSettingsAsync();
        Assert.True(settings.AppSettings.KalmanFilterEnabled);
        Assert.Equal(10, settings.AppSettings.Accuracy);
    }

    // -----------------------------------------------------------------------
    // Traducciones
    // -----------------------------------------------------------------------

    [Fact]
    public void ElIdiomaGuardadoMandaYSinElSeUsaElDelSistema()
    {
        var settings = new SettingsService();
        settings.AppSettings.Language = "";
        var t = new TranslationService(settings);
        var system = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "es" ? "es" : "en";
        Assert.Equal(system, t.GetCurrentLanguage());
        Assert.Equal(system, settings.GetSetting("Language"));   // y se guarda

        t.SetLanguage("en");
        Assert.Equal("en", t.GetCurrentLanguage());
        Assert.Equal("en", new TranslationService(settings).GetCurrentLanguage());
        Assert.Equal("Distance", t.Translate("Distancia"));

        t.SetLanguage("es");
        Assert.Equal("Distancia", t.Translate("Distancia"));
    }

    [Fact]
    public void IdiomasYSusNombres()
    {
        var t = new TranslationService(new SettingsService());
        Assert.Equal(["es", "en"], t.GetSupportedLanguages());
        Assert.Equal("Español", t.GetLanguageName("es"));
        Assert.Equal("English", t.GetLanguageName("en"));
        Assert.Equal("English", t.GetLanguageName("fr"));
    }

    [Fact]
    public void FraseSinTraduccionSaleTalCual()
    {
        Assert.Equal("Frase que no existe", Translations.Translate("Frase que no existe", "en"));
        Assert.Equal("Distancia", Translations.Translate("Distancia", "fr"));
    }

    [Fact]
    public void CadaFraseTieneCastellanoEInglesNoVacios()
    {
        Assert.NotEmpty(Translations.TranslationDict);
        foreach (var (key, byLang) in Translations.TranslationDict)
        {
            // Sin fila «es» vale la propia clave, que ya esta en castellano.
            var es = byLang.TryGetValue("es", out var e) ? e : key;
            Assert.False(string.IsNullOrWhiteSpace(es), $"{key}: es vacio");
            Assert.True(byLang.TryGetValue("en", out var en) && !string.IsNullOrWhiteSpace(en), $"{key}: falta en");
            Assert.Equal(Holes(es), Holes(en!));
        }
    }

    [GeneratedRegex(@"\{(\d+)")]
    private static partial Regex Hole();

    private static string Holes(string text) => string.Join(",", Hole().Matches(text).Select(m => m.Groups[1].Value).Distinct().Order());

    private static IEnumerable<string> AppSources(DirectoryInfo dir)
    {
        foreach (var f in dir.EnumerateFiles("*.cs"))
            yield return f.FullName;
        foreach (var sub in dir.EnumerateDirectories())
        {
            if (sub.Name is "bin" or "obj" or "Hiker.Tests" or "constitution" or "releases" or ".git")
                continue;
            foreach (var f in AppSources(sub))
                yield return f;
        }
    }

    [Fact]
    public void TodaFraseQuePideLaAppEstaTraducidaAlIngles()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(dir!.FullName, "Hiker.csproj")))
            dir = dir.Parent;

        var used = new HashSet<string>();
        foreach (var file in AppSources(dir))
        {
            foreach (Match m in Regex.Matches(File.ReadAllText(file), @"(?:Translate|\bL|\bT)\(""((?:[^""\\]|\\.)+)""\)"))
                used.Add(Regex.Unescape(m.Groups[1].Value));
        }

        Assert.True(used.Count > 50, $"solo {used.Count} frases: ¿ha cambiado la forma de traducir?");
        var missing = used.Where(p => !(Translations.TranslationDict.TryGetValue(p, out var l) && l.ContainsKey("en"))).ToList();
        Assert.True(missing.Count == 0, "Sin ingles: " + string.Join(" | ", missing));
    }
}
