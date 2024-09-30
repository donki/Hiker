using System.Globalization;

namespace Hiker.Services
{

    public class TranslationService
    {

        private string _currentLanguage;

        public TranslationService(SettingsService SettingsService)
        {
            _currentLanguage = SettingsService.AppSettings.Language;
        }

        public string Translate(string nativeWord)
        {

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
