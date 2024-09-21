namespace Hiker.Services
{
    public class GeolocationService
    {
        private readonly SettingsService _settingsService;
        private bool isListening = false;

        public delegate void Location_Changed(Location point);

        public Location_Changed? OnLocationChangedDelegate;

        public async Task ListeningStartAsync()
        {
            if (isListening) { return; }
            isListening = true;
            Geolocation.LocationChanged += Geolocation_LocationChanged;
            var request = new GeolocationListeningRequest(GeolocationAccuracy.Best, TimeSpan.FromSeconds(_settingsService.AppSettings.TimerInterval));
            var success = await Geolocation.StartListeningForegroundAsync(request);
        }

        public async Task ListeningStopAsync()
        {
            Geolocation.LocationChanged -= Geolocation_LocationChanged;
            Geolocation.StopListeningForeground();
            isListening = false;
        }

        private void Geolocation_LocationChanged(object? sender, GeolocationLocationChangedEventArgs e)
        {
            if (OnLocationChangedDelegate != null)
            {
                OnLocationChangedDelegate(e.Location);
            }
            else
            {
                ListeningStopAsync().Wait();
            }
        }

        public GeolocationService(SettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        public async Task<Location?> GetCurrentLocationAsync()
        {
            try
            {
                return await MainThread.InvokeOnMainThreadAsync(async () =>
                {

                    var location = await Geolocation.GetLocationAsync(new GeolocationRequest
                    {
                        DesiredAccuracy = GeolocationAccuracy.Best
                    });

                    return location;
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error obteniendo la ubicación: {ex.Message}");
                return null;
            }
        }
    }
}
