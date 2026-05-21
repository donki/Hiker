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

            if (track != null)
            {
                Console.WriteLine($"Nombre de la ruta: {track.Name}");
                Console.WriteLine($"N�mero de segmentos: {track.Segments.Count}");


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

        public async Task<List<RouteData>> GetAllRoutesAsync()
        {
            // Implementación básica - devuelve rutas guardadas
            var routes = new List<RouteData>();
            
            try
            {
                // Aquí implementarías la lógica para cargar rutas guardadas
                // Por ahora devolvemos una lista vacía
                return routes;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading routes: {ex.Message}");
                return routes;
            }
        }

        public async Task DeleteRouteAsync(string routeName)
        {
            try
            {
                // Implementación básica para eliminar ruta
                // Aquí implementarías la lógica para eliminar la ruta del almacenamiento
                System.Diagnostics.Debug.WriteLine($"Deleting route: {routeName}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error deleting route: {ex.Message}");
                throw;
            }
        }

    }



}
