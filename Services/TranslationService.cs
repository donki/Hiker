using System.Globalization;

namespace Hiker.Services
{
    public class TranslationService
    {
        private string? _currentLanguage = null;
        public SettingsService SettingsService { get; }

        public TranslationService(SettingsService settingsService)
        {
            this.SettingsService = settingsService;
            // Inicializar automáticamente con el idioma del sistema
            InitializeLanguage();
        }

        private void InitializeLanguage()
        {
            // Intentar obtener el idioma guardado en configuración
            var savedLanguage = SettingsService.GetSetting("Language");
            
            if (!string.IsNullOrEmpty(savedLanguage))
            {
                _currentLanguage = savedLanguage;
            }
            else
            {
                // Si no hay idioma guardado, usar el del sistema
                _currentLanguage = GetSupportedLanguage();
                // Guardar la selección automática
                SettingsService.SaveSetting("Language", _currentLanguage);
            }
        }

        public string Translate(string nativeWord)
        {
            if (_currentLanguage == null)
            {
                _currentLanguage = GetSupportedLanguage();
            }
            return Translations.Translate(nativeWord, _currentLanguage);
        }

        public void SetLanguage(string language)
        {
            _currentLanguage = language;
            // Guardar el idioma seleccionado
            SettingsService.SaveSetting("Language", language);
        }

        public string GetCurrentLanguage()
        {
            return _currentLanguage ?? GetSupportedLanguage();
        }

        private string GetSystemLanguage()
        {
            try
            {
                var culture = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
                return culture.ToLowerInvariant();
            }
            catch
            {
                return "en"; // Fallback a inglés si hay error
            }
        }

        private string GetSupportedLanguage()
        {
            var systemLang = GetSystemLanguage();
            // Idiomas oficiales del proyecto: castellano e inglés (constitucion seccion 8).
            return systemLang == "es" ? "es" : "en";
        }

        public string[] GetSupportedLanguages()
        {
            return new[] { "es", "en" };
        }

        public string GetLanguageName(string code)
        {
            return code switch
            {
                "es" => "Español",
                _ => "English"
            };
        }
    }
}


