namespace Hiker.Services
{
    /// <summary>
    /// Adapter para usar FallbackGeolocationService con la interfaz de GeolocationService
    /// </summary>
    public class GeolocationServiceAdapter : IAsyncDisposable, IDisposable
    {
        private readonly FallbackGeolocationService _fallbackService;

        public delegate void Location_Changed(Location point);
        public event Location_Changed? OnLocationChangedDelegate;
        public string NativeMode => _fallbackService.NativeMode;

        public GeolocationServiceAdapter(FallbackGeolocationService fallbackService)
        {
            _fallbackService = fallbackService;
            _fallbackService.OnLocationChangedDelegate += OnLocationChanged;
        }

        private void OnLocationChanged(Location location)
        {
            OnLocationChangedDelegate?.Invoke(location);
        }

        public async Task<bool> ListeningStartAsync()
        {
            return await _fallbackService.ListeningStartAsync();
        }

        public async Task ListeningStopAsync()
        {
            await _fallbackService.ListeningStopAsync();
        }

        public async Task<Location?> GetCurrentLocationAsync()
        {
            return await _fallbackService.GetCurrentLocationAsync();
        }

        public async ValueTask DisposeAsync()
        {
            if (_fallbackService != null)
            {
                _fallbackService.OnLocationChangedDelegate -= OnLocationChanged;
                await _fallbackService.DisposeAsync();
            }
        }

        public void Dispose()
        {
            DisposeAsync().AsTask().Wait(1000);
        }
    }
}