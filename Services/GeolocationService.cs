namespace Hiker.Services
{
    public class GeolocationService
    {
        public async Task<Location?> GetCurrentLocationAsync()
        {
            try
            {
                var location = await Geolocation.GetLastKnownLocationAsync();

                if (location == null)
                {
                    location = await Geolocation.GetLocationAsync(new GeolocationRequest
                    {
                        DesiredAccuracy = GeolocationAccuracy.High,
                        Timeout = TimeSpan.FromSeconds(30)
                    });
                }

                return location;
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

