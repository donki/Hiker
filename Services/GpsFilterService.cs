using Hiker.Helpers;

namespace Hiker.Services
{
    public class GpsFilterService
    {
        private KalmanFilter kalmanFilter;
        private Queue<Location> locationWindow = new Queue<Location>(); // Ventana para el promedio móvil
        private SettingsService _settingsService;
        private Location lastLocation;

        public GpsFilterService(SettingsService settingsService)
        {
            _settingsService = settingsService;
            kalmanFilter = new KalmanFilter((float)_settingsService.AppSettings.MaxSpeed);
        }

        // Método para procesar una lista de puntos GPS
        public List<Location> ApplyFilters(List<Location> locations)
        {
            List<Location> filteredLocations = new List<Location>();

            foreach (var location in locations)
            {
                var filteredLocation = ProcessLocation(location);
                if (filteredLocation != null)
                {
                    filteredLocations.Add(filteredLocation);
                }
            }

            return filteredLocations;
        }

        // Método para procesar un solo punto GPS
        public Location ProcessLocation(Location newLocation)
        {
            // Aplicar el filtro Kalman si está habilitado
            if (_settingsService.AppSettings.KalmanFilterEnabled)
            {
                kalmanFilter.Process(newLocation.Latitude, newLocation.Longitude, (float)newLocation.Accuracy, DateTime.UtcNow.Ticks);
            }

            // Aplicar el filtro de precisión si está habilitado
            if (_settingsService.AppSettings.MinAccuracy > 0)
            {
                if (newLocation.Accuracy < _settingsService.AppSettings.MinAccuracy)
                {
                    newLocation.Accuracy = _settingsService.AppSettings.MinAccuracy;
                }
            }

            // Verificar si el nuevo punto es válido con el filtro de distancia mínima
            if (_settingsService.AppSettings.DistanceFilterEnabled && lastLocation != null && !IsDistanceValid(newLocation))
            {
                return lastLocation; // Si el punto no es válido, devolvemos el último punto conocido
            }

            // Actualizar la última ubicación conocida
            lastLocation = new Location(kalmanFilter.Latitude, kalmanFilter.Longitude);
            lastLocation.Altitude = newLocation.Altitude;
            lastLocation.Accuracy = newLocation.Accuracy;
            lastLocation.Speed = newLocation.Speed;
            lastLocation.Course = newLocation.Course;

            // Añadir la nueva ubicación filtrada a la ventana del promedio móvil si está habilitado
            if (_settingsService.AppSettings.AverageFilterEnabled)
            {
                locationWindow.Enqueue(lastLocation);
                if (locationWindow.Count > _settingsService.AppSettings.WindowSize)
                {
                    locationWindow.Dequeue(); // Mantener la ventana en el tamaño adecuado
                }

                // Aplicar Promedio Móvil
                return GetSmoothedLocation();
            }

            return lastLocation; // Si el filtro de promedio móvil no está habilitado, devolver el punto actual
        }

        // Filtro de distancia mínima
        private bool IsDistanceValid(Location newLocation)
        {
            if (lastLocation == null) return true;

            double distance = Location.CalculateDistance(lastLocation.Latitude, lastLocation.Longitude,
                                                         newLocation.Latitude, newLocation.Longitude,
                                                         DistanceUnits.Kilometers) * 1000; // Convertir a metros

            double timeInSeconds = (DateTime.UtcNow - lastLocation.Timestamp).TotalSeconds;
            double speed = distance / timeInSeconds;

            return speed <= _settingsService.AppSettings.MaxSpeed; // Comprobar si la velocidad es menor o igual a la velocidad máxima permitida
        }

        // Promedio móvil para suavizar la posición
        private Location GetSmoothedLocation()
        {
            double avgLat = locationWindow.Average(loc => loc.Latitude);
            double avgLon = locationWindow.Average(loc => loc.Longitude);
            double avgAlt = locationWindow.Average(loc => loc.Altitude.Value);

            var location = new Location(avgLat, avgLon);
            location.Altitude = avgAlt;
            location.Accuracy = lastLocation.Accuracy;
            location.Speed = lastLocation.Speed;
            location.Course = lastLocation.Course;

            return location;
        }
    }

}
