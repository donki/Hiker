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

        private async Task<bool> StartFallbackListeningAsync()
        {
            try
            {
                NativeMode = "Fallback";
                _isListening = true;
                
                // Simular ubicaciones para desarrollo
                _ = Task.Run(async () =>
                {
                    var madrid = new Location(40.4168, -3.7038)
                    {
                        Accuracy = 10,
                        Timestamp = DateTimeOffset.Now
                    };

                    while (_isListening && !_disposed)
                    {
                        try
                        {
                            if (!_locationWriter.TryWrite(madrid))
                            {
                                System.Diagnostics.Debug.WriteLine("Location buffer full");
                            }
                            await Task.Delay(1000, _cancellationTokenSource.Token);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                    }
                });

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Fallback geolocation failed: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> StartNativeListeningAsync()
        {
            try
            {
                // Verificar permisos primero
                var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
                if (status != PermissionStatus.Granted)
                {
                    status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                    if (status != PermissionStatus.Granted)
                    {
                        return false;
                    }
                }

                Geolocation.LocationChanged += Geolocation_LocationChanged;
                
                var request = new GeolocationListeningRequest(
                    GeolocationAccuracy.Best, 
                    TimeSpan.FromMilliseconds(Math.Max(100, _settingsService.AppSettings.TimerInterval * 1000))
                );
                
                return await Geolocation.StartListeningForegroundAsync(request);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error starting native geolocation: {ex.Message}");
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
            
            // Usar TryWrite para evitar bloqueos
            if (!_locationWriter.TryWrite(e.Location))
            {
                System.Diagnostics.Debug.WriteLine("Location buffer full, dropping location");
            }
        }

        public async Task<Location?> GetCurrentLocationAsync()
        {
            if (_disposed) return null;

            try
            {
                var request = new GeolocationRequest
                {
                    DesiredAccuracy = GeolocationAccuracy.Best,
                    Timeout = TimeSpan.FromSeconds(Math.Max(3, _settingsService.AppSettings.TimerInterval))
                };

                return await Geolocation.GetLocationAsync(request, _cancellationTokenSource.Token);
            }
            catch (System.Runtime.InteropServices.COMException comEx) when (comEx.HResult == -2147221164)
            {
                System.Diagnostics.Debug.WriteLine($"COM error getting location, using fallback: {comEx.Message}");
                return GetFallbackLocation();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting current location, using fallback: {ex.Message}");
                return GetFallbackLocation();
            }
        }

        private Location GetFallbackLocation()
        {
            // Madrid como ubicación por defecto
            return new Location(40.4168, -3.7038)
            {
                Accuracy = 1000,
                Timestamp = DateTimeOffset.Now
            };
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
