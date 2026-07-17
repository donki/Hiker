using Hiker.Models;
using Hiker.Helpers;
using System.Text;

namespace Hiker.Services
{
    public class RouteService
    {

        public bool isRecording = false;
        public double totalDistance;
        public double minElevation;
        public double maxElevation;
        public double stepDistance = 1;
        public double desnivel;
        public TimeSpan estimatedTime;
        public List<ElevationPoint> elevationData = new List<ElevationPoint>();
        private string routeName;
        private List<Location> locations = new List<Location>();
        private readonly SettingsService settingsService;

        private string GpxContent { get; set; } = string.Empty;

        public async Task<RouteData> SetRoute(string gpxContent)
        {
            GpxContent = gpxContent;
            return await ProcessGpxData(gpxContent);
        }

        public string GetRoute()
        {
            return GpxContent;
        }


        public RouteService(SettingsService settingsService)
        {
            this.settingsService = settingsService;
        }

        public async Task SetLocations(List<Location> locations)
        {
            this.locations.AddRange(locations);
        }

        public async Task<RouteData> GetRouteData()
        {
            return await ProcessData(await GetGPXFileHelper());
        }

        public async Task<MemoryStream> SaveTrack(string routeName)
        {

            var tmp = await SaveTrack(this.locations, routeName);


            return new MemoryStream(Encoding.UTF8.GetBytes(tmp));

        }

        public async Task<string> SaveTrack(List<Location> locations, string routeName)
        {
            var defaultRouteName = routeName;

            GPXFileHelper gpxFile = CreateGPXFileHelper(locations, defaultRouteName);

            return gpxFile.ToXML();

        }

        public async Task<GPXFileHelper> GetGPXFileHelper()
        {

            return CreateGPXFileHelper(this.locations, "");


        }

        private static GPXFileHelper CreateGPXFileHelper(List<Location> locations, string defaultRouteName)
        {
            var gpxFile = new GPXFileHelper();
            gpxFile.Creator = "Hiker";

            var track = new Track();
            track.Name = defaultRouteName;

            gpxFile.Tracks.Add(track);

            var segments = new TrackSegment();
            track.Segments.Add(segments);

            foreach (var location in locations)
            {
                var trackpoint = new TrackPoint(location.Latitude, location.Longitude, location.Altitude, location.Timestamp.DateTime);
                segments.TrackPoints.Add(trackpoint);
            }

            return gpxFile;
        }

        private async Task<RouteData?> ProcessGpxData(MemoryStream gpxContent)
        {
            try
            {


                var gpxFile = GPXFileHelper.FromStream(gpxContent);
                return await ProcessData(gpxFile);

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al procesar el archivo GPX: {ex.Message}");
            }
            return null;

        }

        private async Task<RouteData?> ProcessGpxData(string gpxContent)
        {
            try
            {


                var gpxFile = GPXFileHelper.FromXML(gpxContent);
                return await ProcessData(gpxFile);

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al procesar el archivo GPX: {ex.Message}");
            }
            return null;

        }

        private async Task<RouteData> ProcessData(GPXFileHelper gpxFile)
        {
            var track = gpxFile.Tracks.FirstOrDefault();
            if (track is null)
            {
                // GPX vacio o invalido: se devuelve una ruta neutra en vez de reventar con
                // NullReferenceException al desreferenciar el track.
                return new RouteData { routeName = string.Empty, elevationData = new List<ElevationPoint>() };
            }

            routeName = track.Name;

            double totalDistanceInMeters = 0;
            double? prevLatitude = null;
            double? prevLongitude = null;
            minElevation = double.MaxValue;
            maxElevation = double.MinValue;

            DateTime? startTime = null;
            DateTime? endTime = null;
            elevationData.Clear();
            foreach (var segment in track.Segments)
            {
                foreach (var point in segment.TrackPoints)
                {
                    var lat = (double)point.Latitude;
                    var lon = (double)point.Longitude;
                    DateTime timestamp = point.Time;


                    if (prevLatitude.HasValue && prevLongitude.HasValue)
                    {
                        double distance = Location.CalculateDistance(prevLatitude.Value, prevLongitude.Value, lat, lon, DistanceUnits.Kilometers);
                        totalDistanceInMeters += distance * 1000;
                    }

                    prevLatitude = lat;
                    prevLongitude = lon;


                    var elevation = (double)point.Elevation;
                    if (elevation < minElevation)
                    {
                        minElevation = elevation;
                    }
                    if (elevation > maxElevation)
                    {
                        maxElevation = elevation;
                    }

                    elevationData.Add(new ElevationPoint
                    {
                        Distance = (double)totalDistanceInMeters / 1000,
                        Elevation = (double)elevation
                    });



                    var time = point.Time;
                    if (startTime == null)
                    {
                        startTime = time;
                    }
                    endTime = time;

                }
            }

            desnivel = maxElevation - minElevation;
            totalDistance = totalDistanceInMeters / 1000;
            double stepDistance = (int)(totalDistance / 10);
            Console.WriteLine("Distancia total: " + totalDistance + " km");


            if (startTime.HasValue && endTime.HasValue)
            {
                estimatedTime = endTime.Value - startTime.Value;
            }
            else
            {
                var tmp = totalDistance / settingsService.AppSettings.MaxSpeed;
                estimatedTime = TimeSpan.FromHours(tmp);
            }


            return new RouteData
            {
                desnivel = desnivel,
                elevationData = elevationData,
                maxElevation = maxElevation,
                minElevation = minElevation,
                estimatedTime = estimatedTime,
                stepDistance = stepDistance,
                totalDistance = totalDistance,
                routeName = routeName
            };
        }

        // --- Persistencia real de rutas -----------------------------------------------------
        // Las rutas se guardan como ficheros GPX en el almacenamiento privado de la app. Antes
        // estos metodos eran stubs que no escribian ni borraban nada (el guardado "mentia").

        /// <summary>Carpeta privada donde viven los GPX guardados. Se crea si no existe.</summary>
        public static string RoutesDirectory
        {
            get
            {
                var dir = Path.Combine(FileSystem.AppDataDirectory, "routes");
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        private static string RoutePath(string routeName)
            => Path.Combine(RoutesDirectory, FileHelper.NormalizeFileName(routeName) + ".gpx");

        /// <summary>Nombre de la ruta que la pantalla de Rutas pide dibujar en el mapa de GPS.</summary>
        public static string? PendingRouteToLoad { get; set; }

        /// <summary>Fecha de guardado de una ruta (fecha de escritura de su fichero GPX).</summary>
        public DateTime GetRouteDate(string routeName)
        {
            var path = RoutePath(routeName);
            return File.Exists(path) ? File.GetLastWriteTime(path) : DateTime.Now;
        }

        /// <summary>Escribe la ruta grabada como GPX en disco. Devuelve la ruta del fichero.</summary>
        public async Task<string> SaveRouteAsync(List<Location> points, string routeName)
        {
            if (points is null || points.Count == 0)
                throw new InvalidOperationException("No hay puntos que guardar.");

            var gpx = CreateGPXFileHelper(points, routeName);
            var path = RoutePath(routeName);
            await File.WriteAllTextAsync(path, gpx.ToXML());
            return path;
        }

        /// <summary>Lista las rutas guardadas, parseando cada GPX para sus metricas.</summary>
        public async Task<List<RouteData>> GetAllRoutesAsync()
        {
            var routes = new List<RouteData>();

            try
            {
                foreach (var file in Directory.EnumerateFiles(RoutesDirectory, "*.gpx"))
                {
                    try
                    {
                        var xml = await File.ReadAllTextAsync(file);
                        var data = await ProcessGpxData(xml);
                        if (data is not null)
                        {
                            // El nombre visible es el del fichero: es el que el usuario puso al guardar.
                            data.routeName = Path.GetFileNameWithoutExtension(file);
                            routes.Add(data);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Ruta ilegible {file}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error listing routes: {ex.Message}");
            }

            return routes;
        }

        /// <summary>Devuelve los puntos (lat/lon) de una ruta guardada, para dibujarla en el mapa.</summary>
        public async Task<List<Location>> LoadRouteLocationsAsync(string routeName)
        {
            var result = new List<Location>();
            var path = RoutePath(routeName);
            if (!File.Exists(path))
                return result;

            var gpx = GPXFileHelper.FromXML(await File.ReadAllTextAsync(path));
            foreach (var segment in gpx.Tracks.SelectMany(t => t.Segments))
            {
                foreach (var p in segment.TrackPoints)
                {
                    result.Add(new Location((double)p.Latitude, (double)p.Longitude, new DateTimeOffset(p.Time))
                    {
                        Altitude = (double)p.Elevation
                    });
                }
            }
            return result;
        }

        public async Task DeleteRouteAsync(string routeName)
        {
            try
            {
                var path = RoutePath(routeName);
                if (File.Exists(path))
                    File.Delete(path);
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error deleting route: {ex.Message}");
                throw;
            }
        }

    }



}
