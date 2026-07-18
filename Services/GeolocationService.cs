using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Hiker.Services
{
    public class GeolocationService : IAsyncDisposable, IDisposable
    {
        private readonly SettingsService _settingsService;
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private readonly Channel<Location> _locationChannel;
        private readonly ChannelWriter<Location> _locationWriter;
        private readonly ChannelReader<Location> _locationReader;
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private readonly Task _processingTask;
        private volatile bool _isListening = false;
        private volatile bool _disposed = false;

        public delegate void Location_Changed(Location point);
        public event Location_Changed? OnLocationChangedDelegate;
        public string NativeMode { get; private set; } = "Off";

        public GeolocationService(SettingsService settingsService)
        {
            _settingsService = settingsService;
            
            // Usar Channel para mejor rendimiento en .NET 9
            var options = new BoundedChannelOptions(1000)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false
            };
            
            _locationChannel = Channel.CreateBounded<Location>(options);
            _locationWriter = _locationChannel.Writer;
            _locationReader = _locationChannel.Reader;
            
            // Iniciar tarea de procesamiento
            _processingTask = ProcessLocationChannelAsync(_cancellationTokenSource.Token);
        }

        public async Task<bool> ListeningStartAsync()
        {
            if (_disposed || _isListening) return _isListening;

            await _semaphore.WaitAsync(_cancellationTokenSource.Token);
            try
            {
                if (_isListening) return true;

                var success = false;
                try
                {
                    success = await StartNativeListeningAsync();
                    if (success)
                    {
                        _isListening = true;
                        NativeMode = "Native";
                    }
                }
                catch (System.Runtime.InteropServices.COMException comEx) when (comEx.HResult == -2147221164) // REGDB_E_CLASSNOTREG
                {
                    System.Diagnostics.Debug.WriteLine($"COM class not registered, using fallback: {comEx.Message}");
                    success = await StartFallbackListeningAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Native geolocation failed, using fallback: {ex.Message}");
                    success = await StartFallbackListeningAsync();
                }

                return success;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        // Antes esto SIMULABA estar en Madrid (40.4168, -3.7038) inyectando esa posicion
        // cada segundo, lo que hacia que la app "se posicionara" en Madrid de forma falsa.
        // Se elimina la simulacion: si la geolocalizacion nativa no arranca, se informa del
        // fallo de forma honesta en vez de fingir una ubicacion.
        private Task<bool> StartFallbackListeningAsync()
        {
            System.Diagnostics.Debug.WriteLine("Geolocalizacion nativa no disponible; sin ubicacion.");
            return Task.FromResult(false);
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
                // Verificar permisos primero
                var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
                LogInfo($"Permiso ubicacion (check) = {status}");
                if (status != PermissionStatus.Granted)
                {
                    status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                    LogInfo($"Permiso ubicacion (request) = {status}");
                    if (status != PermissionStatus.Granted)
                    {
                        return false;
                    }
                }

                Geolocation.LocationChanged += Geolocation_LocationChanged;

                // Escucha continua a precision Media: en tablets wifi / interiores el proveedor de
                // red (fused) entrega posiciones aunque el GPS no fije; el detalle fino llega cuando
                // hay senal GPS. Con Best (solo GPS) la traza se quedaba sin puntos en interiores.
                var request = new GeolocationListeningRequest(
                    GeolocationAccuracy.Medium,
                    TimeSpan.FromMilliseconds(Math.Max(1000, _settingsService.AppSettings.TimerInterval * 1000))
                );

                var started = await Geolocation.StartListeningForegroundAsync(request);
                LogInfo($"StartListeningForeground = {started}");
                return started;
            }
            catch (Exception ex)
            {
                LogInfo($"Error starting native geolocation: {ex}");
                return false;
            }
        }

        public async Task ListeningStopAsync()
        {
            if (_disposed || !_isListening) return;

            await _semaphore.WaitAsync(_cancellationTokenSource.Token);
            try
            {
                if (!_isListening) return;

                if (Geolocation.IsListeningForeground)
                {
                    Geolocation.LocationChanged -= Geolocation_LocationChanged;
                    Geolocation.StopListeningForeground();
                }

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

        private void Geolocation_LocationChanged(object? sender, GeolocationLocationChangedEventArgs e)
        {
            if (_disposed || e.Location == null) return;

            LogInfo($"LocationChanged: {e.Location.Latitude:F5},{e.Location.Longitude:F5} acc={e.Location.Accuracy}");
            // Usar TryWrite para evitar bloqueos
            if (!_locationWriter.TryWrite(e.Location))
            {
                LogInfo("Location buffer full, dropping location");
            }
        }

        public async Task<Location?> GetCurrentLocationAsync()
        {
            if (_disposed) return null;

            try
            {
                // Para el primer fix se usa precision Media (proveedor de red / fused de Play
                // Services): en interiores o en tablets wifi consigue la ubicacion en segundos,
                // mientras que Best (solo GPS) suele agotar el tiempo sin fijar. La precision fina
                // ya la aporta el listening continuo de Best una vez en marcha. El timeout de 3 s
                // anterior era demasiado corto y devolvia null casi siempre.
                var request = new GeolocationRequest
                {
                    DesiredAccuracy = GeolocationAccuracy.Medium,
                    Timeout = TimeSpan.FromSeconds(25)
                };

                LogInfo("GetCurrentLocation: pidiendo fix (Medium, 25s)...");
                var location = await Geolocation.GetLocationAsync(request, _cancellationTokenSource.Token);
                LogInfo($"GetCurrentLocation: resultado = {(location != null ? $"{location.Latitude:F5},{location.Longitude:F5}" : "null")}");
                return location ?? await Geolocation.GetLastKnownLocationAsync();
            }
            catch (Exception ex)
            {
                // Sin fix disponible se devuelve null (antes se fingia Madrid). Como ultimo
                // recurso se intenta la ultima ubicacion conocida por el sistema.
                LogInfo($"Error getting current location: {ex}");
                try
                {
                    return await Geolocation.GetLastKnownLocationAsync();
                }
                catch
                {
                    return null;
                }
            }
        }

        /// <summary>Ultima ubicacion conocida por el sistema, util para centrar el mapa al instante
        /// mientras llega el primer fix del GPS. Devuelve null si no hay ninguna.</summary>
        public async Task<Location?> GetLastKnownLocationAsync()
        {
            if (_disposed) return null;
            try
            {
                return await Geolocation.GetLastKnownLocationAsync();
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
            _disposed = true;

            await ListeningStopAsync();
            
            _cancellationTokenSource.Cancel();
            _locationWriter.Complete();
            
            try
            {
                await _processingTask;
            }
            catch (OperationCanceledException)
            {
                // Esperado
            }

            _cancellationTokenSource.Dispose();
            _semaphore.Dispose();
        }

        public void Dispose()
        {
            DisposeAsync().AsTask().Wait(1000);
        }
    }
}
