using System.ComponentModel;
using Hiker.Services;

namespace Hiker.Presenters;

/// <summary>
/// Configuracion: solo queda el idioma, que se guarda al pulsar. Las etiquetas de la pagina se
/// enlazan a estas propiedades y se repintan todas al cambiar de idioma.
/// </summary>
public class LanguagePresenter : INotifyPropertyChanged
{
    private readonly TranslationService? _translation;

    /// <param name="translation">Puede faltar si la pagina aparece antes que los servicios: entonces, castellano.</param>
    public LanguagePresenter(TranslationService? translation) => _translation = translation;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Traduce una frase; si falta la traduccion, queda la frase en castellano.</summary>
    protected string L(string phrase) => _translation?.Translate(phrase) ?? phrase;

    public virtual string Title => L("Configuración");
    public string LanguageTitle => L("Idioma");
    public string LanguageHint => L("Selecciona tu idioma preferido");

    public bool SpanishActive => (_translation?.GetCurrentLanguage() ?? "es") == "es";

    /// <summary>Cambia el idioma y avisa de que han cambiado todos los textos.</summary>
    public void SetLanguage(string code)
    {
        _translation?.SetLanguage(code);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }
}

/// <summary>Acerca de: informacion, contacto, idioma y las declaraciones de privacidad, licencia y aviso legal.</summary>
public sealed class AboutPresenter : LanguagePresenter
{
    public const string ContactEmail = "jsoladelarosa@gmail.com";

    private readonly string _version;
    private readonly ILinkOpener _links;

    public AboutPresenter(TranslationService? translation, string version, ILinkOpener links) : base(translation)
    {
        _version = version;
        _links = links;
    }

    public override string Title => L("Acerca de");
    public string Version => $"{L("Versión")} {_version}";
    public string ContactTitle => L("Contacto");
    public string PrivacyTitle => L("Privacidad");
    public string PrivacyText => L("Esta aplicación no recopila tus datos personales. La información se procesa en tu dispositivo para la función propia de la app.");
    public string LicenseTitle => L("Licencia");
    public string LicenseText => L("Esta aplicación es software libre distribuido bajo licencia MIT.");
    public string LegalTitle => L("Aviso Legal");
    public string LegalText1 => L("Este software se proporciona «tal cual», sin garantías de ningún tipo. El usuario es responsable del uso adecuado de la aplicación y del cumplimiento de las leyes locales.");
    public string LegalText2 => L("En ningún caso los autores serán responsables de daños directos, indirectos, incidentales o consecuentes que resulten del uso de este software.");
    public string LegalWarning => L("⚠️ Uso bajo su propio riesgo");

    /// <summary>Abre el correo con el asunto puesto; si no hay cliente de correo, lo dice.</summary>
    public async Task ContactAsync(IUserDialogs dialogs)
    {
        try
        {
            await _links.OpenAsync($"mailto:{ContactEmail}?subject=Hiker");
        }
        catch (Exception ex)
        {
            await dialogs.AlertAsync(L("Error"), $"{L("No se pudo abrir el cliente de correo")}: {ex.Message}", "OK");
        }
    }
}
