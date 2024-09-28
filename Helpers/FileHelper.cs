using System.Text.RegularExpressions;

namespace Hiker.Helpers
{
    public class FileHelper
    {

        public static string NormalizeFileName(string fileName)
        {
            // Obtener los caracteres no válidos para un nombre de archivo
            var invalidChars = Path.GetInvalidFileNameChars();

            // Reemplazar los caracteres no válidos con un guion bajo o simplemente eliminarlos
            foreach (var invalidChar in invalidChars)
            {
                fileName = fileName.Replace(invalidChar, '_');
            }

            // Adicionalmente, podemos usar una expresión regular para eliminar cualquier carácter extraño
            fileName = Regex.Replace(fileName, @"\s+", "_"); // Reemplazar espacios por guion bajo

            return fileName;
        }
    }
}
