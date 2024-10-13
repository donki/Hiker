using System.Globalization;

namespace Hiker.Services
{

    public class TranslationService
    {

        private string _currentLanguage = null;

        public SettingsService SettingsService { get; }

        public TranslationService(SettingsService SettingsService)
        {
            this.SettingsService = SettingsService;
        }

        public string Translate(string nativeWord)
        {
            if (_currentLanguage == null)
            {
                _currentLanguage = SettingsService.AppSettings.Language;
            }

            return Translations.Translate(nativeWord, _currentLanguage);

        }

        public void SetLanguage(string language)
        {
            _currentLanguage = language;
        }

        private string GetSystemLanguage()
        {
            var culture = CultureInfo.CurrentCulture.TwoLetterISOLanguageName;
            return culture;
        }
    }

}
