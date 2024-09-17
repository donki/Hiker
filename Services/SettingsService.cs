using System.Text.Json;

namespace Hiker.Services
{
    public class SettingsService
    {
        private const string SettingsKey = "AppSettings";
        // Configuraciones por defecto
        public Settings AppSettings { get; private set; } = new Settings
        {
            MaxSpeed = 3.0,
            TimerInterval = 1,
            IsKalmanFilterEnabled = true
        };

        // Cargar la configuración desde Preferences
        public async Task LoadSettingsAsync()
        {
            // Leer la configuración desde Preferences
            var json = Preferences.Get(SettingsKey, string.Empty);

            if (!string.IsNullOrEmpty(json))
            {
                // Deserializar el JSON almacenado en las preferencias
                AppSettings = JsonSerializer.Deserialize<Settings>(json) ?? new Settings();
            }
        }

        // Guardar la configuración en Preferences
        public async Task SaveSettingsAsync()
        {
            // Serializar la configuración actual a JSON
            var json = JsonSerializer.Serialize(AppSettings);

            // Guardar en Preferences
            Preferences.Set(SettingsKey, json);
        }
    }

    // Clase de configuraciones que se almacenarán
    public class Settings
    {
        public double MaxSpeed { get; set; }
        public int TimerInterval { get; set; }
        public bool IsKalmanFilterEnabled { get; set; } = true;
    }
}
