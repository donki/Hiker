using System.ComponentModel;
using Hiker.Presenters;
using Hiker.Services;

namespace Hiker.Tests;

/// <summary>Configuracion (idioma) y Acerca de, sin pantallas.</summary>
public class LanguageTests
{
    public LanguageTests() => Preferences.Values.Clear();

    private static TranslationService Spanish()
    {
        var t = new TranslationService(new SettingsService());
        t.SetLanguage("es");
        return t;
    }

    [Fact]
    public void CambiarDeIdiomaRepintaTodo()
    {
        var presenter = new LanguagePresenter(Spanish());
        var changed = new List<string?>();
        ((INotifyPropertyChanged)presenter).PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        Assert.True(presenter.SpanishActive);
        Assert.Equal(("Configuración", "Idioma"), (presenter.Title, presenter.LanguageTitle));

        presenter.SetLanguage("en");

        Assert.False(presenter.SpanishActive);
        Assert.Equal([string.Empty], changed);
        Assert.NotEqual("Idioma", presenter.LanguageTitle);
        Assert.NotEqual("Selecciona tu idioma preferido", presenter.LanguageHint);
    }

    [Fact]
    public void SinServicioDeTraduccionQuedaEnCastellano()
    {
        var presenter = new LanguagePresenter(null);
        presenter.SetLanguage("en");
        Assert.True(presenter.SpanishActive);
        Assert.Equal("Selecciona tu idioma preferido", presenter.LanguageHint);
    }

    [Fact]
    public void AcercaDeTieneTodosSusTextosEnLosDosIdiomas()
    {
        var t = Spanish();
        var presenter = new AboutPresenter(t, "2026.10.01.00", new FakeLinks());
        string[] Texts() =>
        [
            presenter.Title, presenter.ContactTitle, presenter.PrivacyTitle, presenter.PrivacyText, presenter.LicenseTitle,
            presenter.LicenseText, presenter.LegalTitle, presenter.LegalText1, presenter.LegalText2, presenter.LegalWarning,
        ];
        var es = Texts();
        Assert.Equal("Versión 2026.10.01.00", presenter.Version);
        Assert.Equal("Acerca de", presenter.Title);

        presenter.SetLanguage("en");
        var en = Texts();

        Assert.All(es.Zip(en), p => Assert.NotEqual(p.First, p.Second));
        Assert.StartsWith("Version", presenter.Version);
    }

    [Fact]
    public async Task ContactarAbreElCorreoYSiNoPuedeLoDice()
    {
        var links = new FakeLinks();
        var dialogs = new FakeDialogs();
        await new AboutPresenter(null, "1", links).ContactAsync(dialogs);
        Assert.Equal(["mailto:jsoladelarosa@gmail.com?subject=Hiker"], links.Opened);

        await new AboutPresenter(null, "1", new BrokenLinks()).ContactAsync(dialogs);
        Assert.Equal(["Error|No se pudo abrir el cliente de correo: sin correo"], dialogs.Shown);
    }

    private sealed class BrokenLinks : ILinkOpener
    {
        public Task OpenAsync(string uri) => throw new InvalidOperationException("sin correo");
    }
}
