namespace Hiker.Models
{
    public class RouteData
    {
        public double totalDistance;
        public double minElevation;
        public double maxElevation;
        public double stepDistance = 100;
        public double desnivel;
        public TimeSpan estimatedTime; // Tiempo estimado en horas para recorrer la ruta
        public List<ElevationPoint> elevationData = new List<ElevationPoint>();
        public string routeName = "";
        public List<Location> locations = new List<Location>();
    }
}
