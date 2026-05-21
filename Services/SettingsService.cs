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

        public string? GetSetting(string key)
        {
            return key switch
            {
                "Language" => AppSettings.Language,
                "MaxSpeed" => AppSettings.MaxSpeed.ToString(),
                "MinAccuracy" => AppSettings.MinAccuracy.ToString(),
                "TimerInterval" => AppSettings.TimerInterval.ToString(),
                "KalmanFilterEnabled" => AppSettings.KalmanFilterEnabled.ToString(),
                "AverageFilterEnabled" => AppSettings.AverageFilterEnabled.ToString(),
                "SpeedFilterEnabled" => AppSettings.SpeedFilterEnabled.ToString(),
                "AccuracyFilterEnabled" => AppSettings.AccuracyFilterEnabled.ToString(),
                "Accuracy" => AppSettings.Accuracy.ToString(),
                "WindowSize" => AppSettings.WindowSize.ToString(),
                "UseNativeGeolocation" => AppSettings.UseNativeGeolocation.ToString(),
                "ShowlocalizationData" => AppSettings.ShowlocalizationData.ToString(),
                _ => null
            };
        }

        public void SaveSetting(string key, string value)
        {
            switch (key)
            {
                case "Language":
                    AppSettings.Language = value;
                    break;
                case "MaxSpeed":
                    if (double.TryParse(value, out var maxSpeed))
                        AppSettings.MaxSpeed = maxSpeed;
                    break;
                case "MinAccuracy":
                    if (double.TryParse(value, out var minAccuracy))
                        AppSettings.MinAccuracy = minAccuracy;
                    break;
                case "TimerInterval":
                    if (int.TryParse(value, out var timerInterval))
                        AppSettings.TimerInterval = timerInterval;
                    break;
                case "KalmanFilterEnabled":
                    if (bool.TryParse(value, out var kalmanFilter))
                        AppSettings.KalmanFilterEnabled = kalmanFilter;
                    break;
                case "AverageFilterEnabled":
                    if (bool.TryParse(value, out var averageFilter))
                        AppSettings.AverageFilterEnabled = averageFilter;
                    break;
                case "SpeedFilterEnabled":
                    if (bool.TryParse(value, out var speedFilter))
                        AppSettings.SpeedFilterEnabled = speedFilter;
                    break;
                case "AccuracyFilterEnabled":
                    if (bool.TryParse(value, out var accuracyFilter))
                        AppSettings.AccuracyFilterEnabled = accuracyFilter;
                    break;
                case "Accuracy":
                    if (int.TryParse(value, out var accuracy))
                        AppSettings.Accuracy = accuracy;
                    break;
                case "WindowSize":
                    if (int.TryParse(value, out var windowSize))
                        AppSettings.WindowSize = windowSize;
                    break;
                case "UseNativeGeolocation":
                    if (bool.TryParse(value, out var useNative))
                        AppSettings.UseNativeGeolocation = useNative;
                    break;
                case "ShowlocalizationData":
                    if (bool.TryParse(value, out var showData))
                        AppSettings.ShowlocalizationData = showData;
                    break;
            }
            
            // Guardar automáticamente los cambios
            _ = SaveSettingsAsync();
        }

        public async Task ResetSettingsAsync()
        {
            try
            {
                // Restablecer a valores por defecto
                AppSettings = new Settings
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

                await SaveSettingsAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error resetting settings: {ex.Message}");
                throw;
            }
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
