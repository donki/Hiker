using Hiker.Helpers;
using System.Collections.Concurrent;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Hiker.Services
{
    public class GpsFilterService : IAsyncDisposable, IDisposable
    {
        private readonly KalmanFilter _kalmanFilter;
        private readonly ConcurrentQueue<Location> _locationWindow = new();
        private readonly SettingsService _settingsService;
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private Location? _lastLocation;
        private Location? _lastValidLocation;
        private long _lastProcessTime = DateTimeOffset.UtcNow.Ticks;
        private volatile bool _disposed = false;

        // Cache optimizado para configuraciones
        private readonly struct FilterConfig
        {
            public readonly bool KalmanEnabled;
            public readonly bool SpeedEnabled;
            public readonly bool AccuracyEnabled;
            public readonly bool AverageEnabled;
            public readonly double MaxSpeed;
            public readonly int MaxAccuracy;
            public readonly int WindowSize;

            public FilterConfig(SettingsService settings)
            {
                var appSettings = settings.AppSettings;
                KalmanEnabled = appSettings.KalmanFilterEnabled;
                SpeedEnabled = appSettings.SpeedFilterEnabled;
                AccuracyEnabled = appSettings.AccuracyFilterEnabled;
                AverageEnabled = appSettings.AverageFilterEnabled;
                MaxSpeed = appSettings.MaxSpeed;
                MaxAccuracy = appSettings.Accuracy;
                WindowSize = Math.Max(1, Math.Min(appSettings.WindowSize, 10));
            }
        }

        private FilterConfig _cachedConfig;

        public GpsFilterService(SettingsService settingsService)
        {
            _settingsService = settingsService;
            _cachedConfig = new FilterConfig(settingsService);
            _kalmanFilter = new KalmanFilter((float)_cachedConfig.MaxSpeed);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateCachedSettings()
        {
            _cachedConfig = new FilterConfig(_settingsService);
        }

        public List<Location> ApplyFilters(IEnumerable<Location> locations)
        {
            if (_disposed) return new List<Location>();

            var filteredLocations = new List<Location>();
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

        public async Task<Location?> ProcessLocationAsync(Location newLocation)
        {
            if (_disposed || newLocation == null) return null;

            await _semaphore.WaitAsync();
            try
            {
                return ProcessLocationInternal(newLocation);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public Location? ProcessLocation(Location newLocation)
        {
            if (_disposed || newLocation == null) return null;

            if (_semaphore.Wait(100)) // Timeout rápido para evitar bloqueos
            {
                try
                {
                    return ProcessLocationInternal(newLocation);
                }
                finally
                {
                    _semaphore.Release();
                }
            }
            
            return null; // Si no puede obtener el lock, descartar
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Location? ProcessLocationInternal(Location newLocation)
        {
            try
            {
                var currentTime = DateTimeOffset.UtcNow.Ticks;
                
                // Actualizar configuraciones cada 10 segundos
                if (currentTime - _lastProcessTime > TimeSpan.TicksPerSecond * 10)
                {
                    UpdateCachedSettings();
                    _lastProcessTime = currentTime;
                }

                // Filtros rápidos de validación
                if (_cachedConfig.AccuracyEnabled && 
                    newLocation.Accuracy.HasValue && 
                    newLocation.Accuracy.Value > _cachedConfig.MaxAccuracy)
                {
                    return null;
                }

                if (_cachedConfig.SpeedEnabled && 
                    _lastValidLocation != null && 
                    !IsSpeedValidOptimized(newLocation, _lastValidLocation))
                {
                    return null;
                }

                Location processedLocation = newLocation;

                // Aplicar filtro Kalman optimizado
                if (_cachedConfig.KalmanEnabled)
                {
                    _kalmanFilter.Process(
                        newLocation.Latitude, 
                        newLocation.Longitude, 
                        (float)(newLocation.Accuracy ?? 10), 
                        newLocation.Timestamp.Ticks
                    );

                    processedLocation = new Location(_kalmanFilter.Latitude, _kalmanFilter.Longitude)
                    {
                        Altitude = newLocation.Altitude,
                        Accuracy = newLocation.Accuracy,
                        Speed = newLocation.Speed,
                        Course = newLocation.Course,
                        Timestamp = newLocation.Timestamp
                    };
                }

                // Aplicar promedio móvil optimizado
                if (_cachedConfig.AverageEnabled)
                {
                    processedLocation = ApplyMovingAverageOptimized(processedLocation);
                }

                _lastLocation = processedLocation;
                _lastValidLocation = processedLocation;
                
                return processedLocation;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error processing location: {ex.Message}");
                return null;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsSpeedValidOptimized(Location newLocation, Location lastLocation)
        {
            try
            {
                // Usar cálculo optimizado de distancia
                const double EarthRadiusKm = 6371.0;
                
                var lat1Rad = lastLocation.Latitude * Math.PI / 180.0;
                var lat2Rad = newLocation.Latitude * Math.PI / 180.0;
                var deltaLatRad = (newLocation.Latitude - lastLocation.Latitude) * Math.PI / 180.0;
                var deltaLonRad = (newLocation.Longitude - lastLocation.Longitude) * Math.PI / 180.0;

                var a = Math.Sin(deltaLatRad / 2) * Math.Sin(deltaLatRad / 2) +
                        Math.Cos(lat1Rad) * Math.Cos(lat2Rad) *
                        Math.Sin(deltaLonRad / 2) * Math.Sin(deltaLonRad / 2);
                
                var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
                var distanceKm = EarthRadiusKm * c;
                var distanceM = distanceKm * 1000;

                var timeSpan = newLocation.Timestamp - lastLocation.Timestamp;
                var timeInSeconds = Math.Max(0.1, timeSpan.TotalSeconds);
                var speedMps = distanceM / timeInSeconds;

                return speedMps <= _cachedConfig.MaxSpeed;
            }
            catch
            {
                return true;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Location ApplyMovingAverageOptimized(Location newLocation)
        {
            _locationWindow.Enqueue(newLocation);

            // Mantener ventana con límite optimizado
            while (_locationWindow.Count > _cachedConfig.WindowSize)
            {
                _locationWindow.TryDequeue(out _);
            }

            var count = _locationWindow.Count;
            if (count == 0) return newLocation;
            if (count == 1) return newLocation;

            // Cálculo vectorizado del promedio
            var locations = _locationWindow.ToArray();
            var sumLat = 0.0;
            var sumLon = 0.0;
            var sumAlt = 0.0;
            var altCount = 0;

            for (int i = 0; i < count; i++)
            {
                var loc = locations[i];
                sumLat += loc.Latitude;
                sumLon += loc.Longitude;
                
                if (loc.Altitude.HasValue)
                {
                    sumAlt += loc.Altitude.Value;
                    altCount++;
                }
            }

            var avgLat = sumLat / count;
            var avgLon = sumLon / count;
            var avgAlt = altCount > 0 ? sumAlt / altCount : newLocation.Altitude ?? 0;

            return new Location(avgLat, avgLon)
            {
                Altitude = avgAlt,
                Accuracy = newLocation.Accuracy,
                Speed = newLocation.Speed,
                Course = newLocation.Course,
                Timestamp = newLocation.Timestamp
            };
        }

        public async Task ResetAsync()
        {
            if (_disposed) return;

            await _semaphore.WaitAsync();
            try
            {
                _lastLocation = null;
                _lastValidLocation = null;
                while (_locationWindow.TryDequeue(out _)) { }
                UpdateCachedSettings();
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public void Reset()
        {
            ResetAsync().Wait(1000);
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;

            await _semaphore.WaitAsync();
            try
            {
                while (_locationWindow.TryDequeue(out _)) { }
            }
            finally
            {
                _semaphore.Release();
                _semaphore.Dispose();
            }
        }

        public void Dispose()
        {
            DisposeAsync().AsTask().Wait(1000);
        }
    }
}
