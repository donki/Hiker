using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using System.Reflection;

namespace Hiker.Services
{
    public static class Translations
    {
        // Diccionario que contiene las frases nativas y sus traducciones por idioma
        public static Dictionary<string, Dictionary<string, string>> TranslationDict = new Dictionary<string, Dictionary<string, string>>();

        static Translations()
        {
            LoadTranslationsFromCsv();
        }

        private static void LoadTranslationsFromCsv()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "Hiker.Resources.Translations.csv"; // Asegúrate de que este nombre coincida con el tuyo

            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    var availableResources = assembly.GetManifestResourceNames();
                    string resourcesList = string.Join(", ", availableResources);
                    throw new FileNotFoundException($"El recurso incrustado {resourceName} no se encontró. Recursos disponibles: {resourcesList}");
                }

                var config = new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HasHeaderRecord = true,
                };

                using (var reader = new StreamReader(stream))
                using (var csv = new CsvReader(reader, config))
                {
                    csv.Read();
                    csv.ReadHeader();
                    while (csv.Read())
                    {
                        var key = csv.GetField<string>("Key");
                        var language = csv.GetField<string>("Language");
                        var translation = csv.GetField<string>("Translation");

                        if (!TranslationDict.ContainsKey(key))
                        {
                            TranslationDict[key] = new Dictionary<string, string>();
                        }

                        TranslationDict[key][language] = translation;
                    }
                }
            }
        }

        public static string Translate(string nativePhrase, string currentLanguage)
        {
            if (TranslationDict.TryGetValue(nativePhrase, out var byLang) &&
                byLang.TryGetValue(currentLanguage, out var translation) &&
                !string.IsNullOrWhiteSpace(translation))
            {
                return translation;
            }

            // Fallback sin fugas (constitucion, seccion 8): la frase nativa ya esta en el idioma
            // base (español), asi que se devuelve tal cual. Nunca se muestra un marcador tecnico
            // como "...wo Translation" al usuario.
            return nativePhrase;
        }
    }
}
