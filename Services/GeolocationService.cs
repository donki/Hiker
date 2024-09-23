using GeolocatorPlugin;
using GeolocatorPlugin.Abstractions;

namespace Hiker.Services
{
    public class GeolocationService
    {
        private readonly SettingsService _settingsService;
        private bool isListening = false;

        public delegate void Location_Changed(Location point);

        public Location_Changed? OnLocationChangedDelegate;
        public string NativeMode = "Off";


        public async Task ListeningStartAsync()
        {
            if (isListening) { return; }
            isListening = true;
            if (_settingsService.AppSettings.UseNativeGeolocation)
            {
                NativeMode = "Native";
                Geolocation.LocationChanged += Geolocation_LocationChanged;
                var request = new GeolocationListeningRequest(GeolocationAccuracy.Best, TimeSpan.FromSeconds(_settingsService.AppSettings.TimerInterval));
                var success = await Geolocation.StartListeningForegroundAsync(request);
            }
            else
            {
                NativeMode = "Cross";
                await CrossGeolocator.Current.StartListeningAsync(TimeSpan.FromSeconds(_settingsService.AppSettings.TimerInterval), 0, true, new ListenerSettings
                {
                    ActivityType = ActivityType.Fitness,
                    AllowBackgroundUpdates = true,
                    DeferLocationUpdates = false,
                    ListenForSignificantChanges = false,
                    PauseLocationUpdatesAutomatically = false,
                    ShowsBackgroundLocationIndicator = true,
                });

                CrossGeolocator.Current.PositionChanged += CrossGeolocator_Current_PositionChanged;
            }
        }

        private void CrossGeolocator_Current_PositionChanged(object? sender, PositionEventArgs e)
        {
            if (OnLocationChangedDelegate != null)
            {
                var newLocation = ConverCrossLocationToLocation(e.Position);
                OnLocationChangedDelegate(newLocation);
            }
            else
            {
                ListeningStopAsync().Wait();
            }
        }

        public async Task ListeningStopAsync()
        {
            if (Geolocation.IsListeningForeground)
            {
                Geolocation.LocationChanged -= Geolocation_LocationChanged;
                Geolocation.StopListeningForeground();
            }

            if (CrossGeolocator.Current.IsListening)
            {
                CrossGeolocator.Current.PositionChanged -= CrossGeolocator_Current_PositionChanged;
                CrossGeolocator.Current.StopListeningAsync();
            }

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
            if (_settingsService.AppSettings.UseNativeGeolocation)
            {
                return await NativeGetCurrentLocationAsync();
            }
            else
            {
                return await CrossGetCurrentLocationAsync();
            }
        }

        public async Task<Location?> CrossGetCurrentLocationAsync()
        {
            try
            {

                var location = await CrossGeolocator.Current.GetPositionAsync(TimeSpan.FromSeconds(_settingsService.AppSettings.TimerInterval), null, true);

                Location reslocation = ConverCrossLocationToLocation(location);

                return reslocation;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error obteniendo la ubicación: {ex.Message}");
                return null;
            }
        }

        private static Location ConverCrossLocationToLocation(Position location)
        {
            var reslocation = new Location();
            reslocation.Latitude = location.Latitude;
            reslocation.Longitude = location.Longitude;
            reslocation.Altitude = location.Altitude;
            reslocation.Course = location.Heading;
            return reslocation;
        }

        public async Task<Location?> NativeGetCurrentLocationAsync()
        {
            try
            {

                var location = await Geolocation.GetLocationAsync(new GeolocationRequest
                {
                    DesiredAccuracy = GeolocationAccuracy.Best,
                    Timeout = TimeSpan.FromSeconds(_settingsService.AppSettings.TimerInterval)
                });

                return location;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error obteniendo la ubicación: {ex.Message}");
                return null;
            }
        }
    }
}
