using System.Globalization;

namespace Hiker.Services
{

    public class TranslationService
    {

        public string Translate(string nativeWord)
        {

            return Translations.Translate(nativeWord);

        }

        private string GetSystemLanguage()
        {
            var culture = CultureInfo.CurrentCulture.TwoLetterISOLanguageName;
            return culture;
        }
    }

}
