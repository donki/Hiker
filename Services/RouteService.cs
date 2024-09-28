using Hiker.Data;
using SharpGPX;
using SharpGPX.GPX1_1;

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
            return await ProcessGpxData(await SaveTrack(""));
        }

        public async Task<MemoryStream> SaveTrack(string routeName)
        {

            return await SaveTrack(this.locations, routeName);
        }

        public async Task<MemoryStream> SaveTrack(List<Location> locations, string routeName)
        {
            var defaultRouteName = routeName;


            var gpxFile = new SharpGPX.GpxClass(GpxVersion.GPX_1_1);
            gpxFile.Creator = "Hiker";

            var track = new trkType();
            track.name = defaultRouteName;

            gpxFile.AddTrack(track);

            var segments = new trksegType();
            track.trkseg.Add(segments);

            foreach (var location in locations)
            {
                var trackpoint = new wptType(location.Latitude, location.Longitude, location.Altitude, location.Timestamp.DateTime);
                segments.trkpt.Add(trackpoint);
            }

            var memoryStream = new MemoryStream();

            gpxFile.ToStream(memoryStream);

            memoryStream.Position = 0;

            return memoryStream;
        }

        private async Task<RouteData?> ProcessGpxData(MemoryStream gpxContent)
        {
            try
            {


                var gpxFile = SharpGPX.GpxClass.FromStream(gpxContent);
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


                var gpxFile = SharpGPX.GpxClass.FromXml(gpxContent);
                return await ProcessData(gpxFile);

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al procesar el archivo GPX: {ex.Message}");
            }
            return null;

        }

        private async Task<RouteData> ProcessData(GpxClass gpxFile)
        {
            var track = gpxFile.Tracks.FirstOrDefault();

            if (track != null)
            {
                Console.WriteLine($"Nombre de la ruta: {track.name}");
                Console.WriteLine($"Número de segmentos: {track.trkseg.Count}");


            }

            routeName = track.name;

            double totalDistanceInMeters = 0;
            double? prevLatitude = null;
            double? prevLongitude = null;
            minElevation = double.MaxValue;
            maxElevation = double.MinValue;

            DateTime? startTime = null;
            DateTime? endTime = null;
            elevationData.Clear();
            foreach (var segment in track.trkseg)
            {
                foreach (var point in segment.trkpt)
                {
                    var lat = (double)point.lat;
                    var lon = (double)point.lon;
                    DateTime timestamp = point.time;


                    if (prevLatitude.HasValue && prevLongitude.HasValue)
                    {
                        double distance = Location.CalculateDistance(prevLatitude.Value, prevLongitude.Value, lat, lon, DistanceUnits.Kilometers);
                        totalDistanceInMeters += distance * 1000;
                    }

                    prevLatitude = lat;
                    prevLongitude = lon;


                    var elevation = (double)point.ele;
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



                    var time = point.time;
                    if (startTime == null)
                    {
                        startTime = time;
                    }
                    endTime = time;

                }
            }

            desnivel = maxElevation - minElevation;
            totalDistance = totalDistanceInMeters / 1000;
            double stepDistance = totalDistance / 10;
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

    }



}
