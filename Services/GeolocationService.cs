using Hiker.Helpers;

namespace Hiker.Services
{
    public class GeolocationService
    {
        private readonly SettingsService _settingsService;
        private Location LastLocation;
        private DateTime LastUpdateTime;
        private KalmanFilter kalmanFilter;

        // Inyectar SettingsService
        public GeolocationService(SettingsService settingsService)
        {
            _settingsService = settingsService;
            kalmanFilter = new KalmanFilter((float)settingsService.AppSettings.MaxSpeed); // Establece el valor predeterminado, se puede ajustar
        }

        public async Task<Location?> GetCurrentLocationAsync()
        {
            try
            {
                // Asegurar que la solicitud de permisos y obtención de ubicación se realiza en el hilo principal
                return await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    if (LastLocation == null)
                    {
                        LastLocation = await Geolocation.GetLastKnownLocationAsync();
                    }

                    var location = await Geolocation.GetLocationAsync(new GeolocationRequest
                    {
                        DesiredAccuracy = GeolocationAccuracy.Best,
                        Timeout = TimeSpan.FromSeconds(_settingsService.AppSettings.TimerInterval) // Usar el valor de configuración para el temporizador
                    });

                    if (location != null)
                    {
                        long currentTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                        // Si está activado el filtro Kalman en los ajustes
                        if (_settingsService.AppSettings.IsKalmanFilterEnabled)
                        {
                            if (kalmanFilter.TimeStamp == 0)
                            {
                                // Si es la primera vez que se usa el filtro, inicializar el estado
                                kalmanFilter.SetState(location.Latitude, location.Longitude, (float)location.Accuracy, currentTimestamp);
                            }
                            else
                            {
                                // Aplicar el filtro Kalman a la nueva ubicación
                                kalmanFilter.Process(location.Latitude, location.Longitude, (float)location.Accuracy, currentTimestamp);
                                var newlocation = new Location(kalmanFilter.Latitude, kalmanFilter.Longitude);
                                newlocation.Altitude = location.Altitude;
                                newlocation.Accuracy = location.Accuracy;
                                newlocation.Speed = location.Speed;
                                newlocation.Course = location.Course;
                            }
                        }

                        // Si es la primera vez o no tenemos una ubicación anterior, guardamos la nueva
                        if (LastLocation == null)
                        {
                            LastLocation = location;
                            LastUpdateTime = DateTime.UtcNow;
                            return location;
                        }

                        // Calcular la distancia entre la nueva ubicación y la última conocida
                        double distance = Location.CalculateDistance(LastLocation, location, DistanceUnits.Kilometers) * 1000; // Convertir a metros

                        // Calcular el tiempo transcurrido desde la última actualización
                        double timeInSeconds = (DateTime.UtcNow - LastUpdateTime).TotalSeconds;

                        // Calcular la velocidad (metros por segundo)
                        double speed = distance / timeInSeconds;

                        // Si la velocidad es razonable, actualizamos la ubicación
                        if (speed <= _settingsService.AppSettings.MaxSpeed) // Usar el valor de configuración para la velocidad máxima
                        {
                            LastLocation = location;
                            LastUpdateTime = DateTime.UtcNow;
                            return location;
                        }
                        else
                        {
                            // Si la velocidad es muy alta, devolvemos la última ubicación conocida
                            return LastLocation;
                        }
                    }

                    return LastLocation; // Si no hay nueva ubicación, devolvemos la última conocida
                });
            }
            catch (Exception ex)
            {
                // Manejo de errores
                Console.WriteLine($"Error obteniendo la ubicación: {ex.Message}");
                return null;
            }
        }
    }
}
