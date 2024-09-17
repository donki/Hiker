namespace Hiker.Services
{
    public class RouteService
    {

        public bool isRecording = false;

        public string GpxContent { get; set; } = string.Empty;

        public void SetRoute(string gpxContent)
        {
            GpxContent = gpxContent;
        }

        public string GetRoute()
        {
            return GpxContent;
        }
    }

}
