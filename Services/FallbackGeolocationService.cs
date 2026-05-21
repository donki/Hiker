using System.Collections.Concurrent;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Channels;

namespace Hiker.Services
{
    public class FallbackGeolocationService : IAsyncDisposable, IDisposable
    {
        private readonly SettingsService _settingsService;
        private readonly HttpClient _httpClient;
        private readonly Channel<Location> _locationChannel;
        private readonly ChannelWriter<Location> _locationWriter;
        private readonly ChannelReader<Location> _locationReader;
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private readonly Task _processingTask;
        private readonly Timer _locationTimer;
        private volatile bool _isListening = false;
        private volatile bool _disposed = false;
        private Location? _lastKnownLocation;

        public delegate void Location_Changed(Location point);
        public event Location_Changed? OnLocationChangedDelegate;
        public string NativeMode { get; private set; } = "Fallback";

        public FallbackGeolocationService(SettingsService settingsService)
        {
            _settingsService = settingsService;
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(10);

            var options = new BoundedChannelOptions(100)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false
            };

            _locationChannel = Channel.CreateBounded<Location>(options);
            _locationWriter = _locationChannel.Writer;
            _locationReader = _locationChannel.Reader;

            _processingTask = ProcessLocationChannelAsync(_cancellationTokenSource.Token);
            _locationTimer = new Timer(SimulateLocationUpdate, null, Timeout.Infinite, Timeout.Infinite);
        }

        public async Task<bool> ListeningStartAsync()
        {
            if (_disposed || _isListening) return _isListening;

            try
            {
                // Intentar obtener ubicación inicial
                var initialLocation = await GetLocationFromIPAsync();
                if (initialLocation != null)
                {
                    _lastKnownLocation = initialLocation;
                    _locationWriter.TryWrite(initialLocation);
                }

                // Iniciar simulación de actualizaciones (para desarrollo/testing)
                var interval = Math.Max(1000, _settingsService.AppSettings.TimerInterval * 1000);
                _locationTimer.Change(interval, interval);

                _isListening = true;
                NativeMode = "Fallback Active";
                
                System.Diagnostics.Debug.WriteLine("Fallback geolocation service started");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error starting fallback geolocation: {ex.Message}");
                return false;
            }
        }

        public async Task ListeningStopAsync()
        {
            if (_disposed || !_isListening) return;

            try
            {
                _locationTimer.Change(Timeout.Infinite, Timeout.Infinite);
                _isListening = false;
                NativeMode = "Fallback Off";
                
                System.Diagnostics.Debug.WriteLine("Fallback geolocation service stopped");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error stopping fallback geolocation: {ex.Message}");
            }
        }

        public async Task<Location?> GetCurrentLocationAsync()
        {
            if (_disposed) return null;

            try
            {
                // Intentar obtener ubicación real primero
                var location = await GetLocationFromIPAsync();
                if (location != null)
                {
                    _lastKnownLocation = location;
                    return location;
                }

                // Fallback a última ubicación conocida
                return _lastKnownLocation ?? CreateDefaultLocation();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting current location: {ex.Message}");
                return _lastKnownLocation ?? CreateDefaultLocation();
            }
        }

        private async Task<Location?> GetLocationFromIPAsync()
        {
            try
            {
                // Usar servicio de geolocalización por IP como fallback
                var response = await _httpClient.GetStringAsync("http://ip-api.com/json/?fields=lat,lon,city,country");
                var data = JsonSerializer.Deserialize<JsonElement>(response);

                if (data.TryGetProperty("lat", out var latElement) && 
                    data.TryGetProperty("lon", out var lonElement))
                {
                    var lat = latElement.GetDouble();
                    var lon = lonElement.GetDouble();

                    return new Location(lat, lon)
                    {
                        Accuracy = 1000, // Precisión baja para IP geolocation
                        Timestamp = DateTimeOffset.Now
                    };
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"IP geolocation failed: {ex.Message}");
            }

            return null;
        }

        private Location CreateDefaultLocation()
        {
            // Madrid como ubicación por defecto
            return new Location(40.4168, -3.7038)
            {
                Accuracy = 10000,
                Timestamp = DateTimeOffset.Now
            };
        }

        private void SimulateLocationUpdate(object? state)
        {
            if (_disposed || !_isListening) return;

            try
            {
                // Simular pequeños movimientos para testing
                if (_lastKnownLocation != null)
                {
                    var random = new Random();
                    var deltaLat = (random.NextDouble() - 0.5) * 0.001; // ~100m
                    var deltaLon = (random.NextDouble() - 0.5) * 0.001;

                    var simulatedLocation = new Location(
                        _lastKnownLocation.Latitude + deltaLat,
                        _lastKnownLocation.Longitude + deltaLon)
                    {
                        Accuracy = 5 + random.Next(10),
                        Timestamp = DateTimeOffset.Now
                    };

                    _locationWriter.TryWrite(simulatedLocation);
                    _lastKnownLocation = simulatedLocation;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in simulated location update: {ex.Message}");
            }
        }

        private async Task ProcessLocationChannelAsync(CancellationToken cancellationToken)
        {
            try
            {
                await foreach (var location in _locationReader.ReadAllAsync(cancellationToken))
                {
                    try
                    {
                        OnLocationChangedDelegate?.Invoke(location);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error processing location: {ex.Message}");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Esperado cuando se cancela
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in location processing task: {ex.Message}");
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;

            await ListeningStopAsync();
            
            _cancellationTokenSource.Cancel();
            _locationWriter.Complete();
            _locationTimer.Dispose();
            
            try
            {
                await _processingTask;
            }
            catch (OperationCanceledException)
            {
                // Esperado
            }

            _httpClient.Dispose();
            _cancellationTokenSource.Dispose();
        }

        public void Dispose()
        {
            DisposeAsync().AsTask().Wait(1000);
        }
    }
}