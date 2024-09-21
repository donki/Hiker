namespace Hiker.Services
{
    public class RouteService
    {

        public bool isRecording = false;

        private string GpxContent { get; set; } = string.Empty;

        private readonly SettingsService _settingsService;
        private Location LastLocation;
        private DateTime LastUpdateTime;


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
