namespace Hiker.Helpers
{
    public class FormatingHelper
    {

        public static string FloatToStr(double value, int decimals = 2)
        {
            // Redondear el valor al número de decimales especificado
            double roundedValue = (double)Math.Round(value, decimals);

            // Convertir a cadena con el formato de decimales especificado
            string formattedValue = roundedValue.ToString($"F{decimals}");

            return formattedValue;
        }
    }
}
