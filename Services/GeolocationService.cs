using System.Threading.Channels;

namespace Hiker.Services
{
    /// <summary>
    /// Ubicacion en vivo para el mapa (no para grabar: eso lo hace el servicio en primer plano).
    /// El GPS y el permiso llegan por interfaz (<see cref="IGeolocation"/> y
    /// <see cref="ILocationPermission"/>) para poder probar la logica sin un movil.
    /// </summary>
    public class GeolocationService : ILocationSource, IAsyncDisposable, IDisposable
    {
        private readonly SettingsService _settingsService;
        private readonly IGeolocation _geolocation;
        private readonly ILocationPermission _permission;
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private readonly ChannelWriter<Location> _locationWriter;
        private readonly ChannelReader<Location> _locationReader;
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private readonly Task _processingTask;
        private volatile bool _isListening = false;
        private volatile bool _disposed = false;

        public event Action<Location>? LocationChanged;
        public string NativeMode { get; private set; } = "Off";

        public GeolocationService(SettingsService settingsService, IGeolocation geolocation, ILocationPermission permission)
        {
            _settingsService = settingsService;
            _geolocation = geolocation;
            _permission = permission;

            // Cola acotada: si la pagina se atasca, se tiran las posiciones viejas, no las nuevas.
            var channel = Channel.CreateBounded<Location>(new BoundedChannelOptions(1000)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false
            });
            _locationWriter = channel.Writer;
            _locationReader = channel.Reader;

            _processingTask = ProcessLocationChannelAsync(_cancellationTokenSource.Token);
        }

        public bool IsListening => _isListening;

        public async Task<bool> ListeningStartAsync()
        {
            if (_disposed || _isListening) return _isListening;

            await _semaphore.WaitAsync(_cancellationTokenSource.Token);
            try
            {
                if (_isListening) return true;

                // Si la escucha nativa no arranca, se dice (false) en vez de fingir una posicion:
                // antes se simulaba estar en Madrid y la app «se posicionaba» alli.
                var success = await StartNativeListeningAsync();
                if (success)
                {
                    _isListening = true;
                    NativeMode = "Native";
                }
                return success;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        // Log visible en logcat tambien en Release (Debug.WriteLine se elimina al compilar Release).
        internal static void LogInfo(string msg)
        {
#if ANDROID
            Android.Util.Log.Info("HikerGeo", msg);
#endif
            System.Diagnostics.Debug.WriteLine(msg);
        }

        private async Task<bool> StartNativeListeningAsync()
        {
            try
            {
                var status = await _permission.CheckAsync();
                LogInfo($"Permiso ubicacion (check) = {status}");
                if (status != PermissionStatus.Granted)
                {
                    status = await _permission.RequestAsync();
                    LogInfo($"Permiso ubicacion (request) = {status}");
                    if (status != PermissionStatus.Granted)
                        return false;
                }

                _geolocation.LocationChanged += Geolocation_LocationChanged;

                // Escucha continua a precision Media: en tablets wifi / interiores el proveedor de
                // red (fused) entrega posiciones aunque el GPS no fije; el detalle fino llega cuando
                // hay senal GPS. Con Best (solo GPS) la traza se quedaba sin puntos en interiores.
                var request = new GeolocationListeningRequest(GeolocationAccuracy.Medium, ListeningInterval);

                var started = await _geolocation.StartListeningForegroundAsync(request);
                LogInfo($"StartListeningForeground = {started}");
                if (!started)
                    _geolocation.LocationChanged -= Geolocation_LocationChanged;
                return started;
            }
            catch (Exception ex)
            {
                LogInfo($"Error starting native geolocation: {ex}");
                _geolocation.LocationChanged -= Geolocation_LocationChanged;
                return false;
            }
        }

        /// <summary>Intervalo de la escucha: el de Configuracion, con un minimo de un segundo.</summary>
        public TimeSpan ListeningInterval =>
            TimeSpan.FromMilliseconds(Math.Max(1000, _settingsService.AppSettings.TimerInterval * 1000));

        public async Task ListeningStopAsync()
        {
            if (_disposed || !_isListening) return;

            await _semaphore.WaitAsync(_cancellationTokenSource.Token);
            try
            {
                if (!_isListening) return;

                _geolocation.LocationChanged -= Geolocation_LocationChanged;
                if (_geolocation.IsListeningForeground)
                    _geolocation.StopListeningForeground();

                _isListening = false;
                NativeMode = "Off";
            }
            finally
            {
                _semaphore.Release();
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
                        LocationChanged?.Invoke(location);
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
        }

        private void Geolocation_LocationChanged(object? sender, GeolocationLocationChangedEventArgs e)
        {
            if (_disposed || e.Location == null) return;

            LogInfo($"LocationChanged: {e.Location.Latitude:F5},{e.Location.Longitude:F5} acc={e.Location.Accuracy}");
            _locationWriter.TryWrite(e.Location);
        }

        public async Task<Location?> GetCurrentLocationAsync()
        {
            if (_disposed) return null;

            try
            {
                // Para el primer fix se usa precision Media (proveedor de red / fused de Play
                // Services): en interiores o en tablets wifi consigue la ubicacion en segundos,
                // mientras que Best (solo GPS) suele agotar el tiempo sin fijar. El timeout de 3 s
                // anterior era demasiado corto y devolvia null casi siempre.
                var request = new GeolocationRequest
                {
                    DesiredAccuracy = GeolocationAccuracy.Medium,
                    Timeout = TimeSpan.FromSeconds(25)
                };

                LogInfo("GetCurrentLocation: pidiendo fix (Medium, 25s)...");
                var location = await _geolocation.GetLocationAsync(request, _cancellationTokenSource.Token);
                LogInfo($"GetCurrentLocation: resultado = {(location != null ? $"{location.Latitude:F5},{location.Longitude:F5}" : "null")}");
                return location ?? await _geolocation.GetLastKnownLocationAsync();
            }
            catch (Exception ex)
            {
                // Sin fix disponible: como ultimo recurso, la ultima ubicacion conocida del sistema.
                LogInfo($"Error getting current location: {ex}");
                return await GetLastKnownLocationAsync();
            }
        }

        /// <summary>Ultima ubicacion conocida por el sistema, util para centrar el mapa al instante
        /// mientras llega el primer fix del GPS. Devuelve null si no hay ninguna.</summary>
        public async Task<Location?> GetLastKnownLocationAsync()
        {
            if (_disposed) return null;
            try
            {
                return await _geolocation.GetLastKnownLocationAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting last known location: {ex.Message}");
                return null;
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;

            await ListeningStopAsync();
            _disposed = true;

            _cancellationTokenSource.Cancel();
            _locationWriter.TryComplete();
            await _processingTask;

            _cancellationTokenSource.Dispose();
            _semaphore.Dispose();
        }

        public void Dispose()
        {
            DisposeAsync().AsTask().Wait(1000);
        }
    }
}
