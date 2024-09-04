namespace Hiker.Services
{
    public class GeolocationService
    {
        public async Task<Location?> GetCurrentLocationAsync()
        {
            try
            {
                // Asegurar que la solicitud de permisos y obtención de ubicación se realiza en el hilo principal
                return await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    var location = await Geolocation.GetLastKnownLocationAsync();

                    if (location == null)
                    {
                        location = await Geolocation.GetLocationAsync(new GeolocationRequest
                        {
                            DesiredAccuracy = GeolocationAccuracy.Best,
                            Timeout = TimeSpan.FromSeconds(120)
                        });
                    }

                    return location;
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

