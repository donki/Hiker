using System.Globalization;
using System.Text.Json;

namespace Hiker.Services
{
    public class SettingsService
    {
        private const string SettingsKey = "AppSettings";
        public Settings AppSettings { get; private set; } = new Settings
        {
            MaxSpeed = 3.0,
            MinAccuracy = 3.0,
            TimerInterval = 1,
            KalmanFilterEnabled = true,
            AverageFilterEnabled = true,
            SpeedFilterEnabled = false,
            WindowSize = 3,
            UseNativeGeolocation = true,
            ShowlocalizationData = true,
            AccuracyFilterEnabled = true,
            Accuracy = 10,
            Language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName
        };

        public async Task LoadSettingsAsync()
        {
            var json = Preferences.Get(SettingsKey, string.Empty);

            if (!string.IsNullOrEmpty(json))
            {
                AppSettings = JsonSerializer.Deserialize<Settings>(json);
            }
        }

        public async Task SaveSettingsAsync()
        {
            var json = JsonSerializer.Serialize(AppSettings);
            Preferences.Set(SettingsKey, json);
        }
    }

    public class Settings
    {
        public double MaxSpeed { get; set; } = 3.0;
        public double MinAccuracy { get; set; } = 3.0;
        public int TimerInterval { get; set; } = 1;
        public bool KalmanFilterEnabled { get; set; } = true;
        public bool AverageFilterEnabled { get; set; } = true;
        public bool SpeedFilterEnabled { get; set; } = true;
        public bool AccuracyFilterEnabled { get; set; } = true;
        public int Accuracy { get; set; } = 10;
        public int WindowSize { get; set; } = 3;
        public bool UseNativeGeolocation { get; set; } = true;
        public bool ShowlocalizationData { get; set; } = false;

        public string Language { get; set; } = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
    }
}
