using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Storage;
using Hiker.Services;
using Microsoft.Extensions.Logging;

namespace Hiker
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            
            try
            {
                builder
                    .UseMauiApp<App>()
                    .UseMauiCommunityToolkit();
                // Tipografia del sistema (constitucion A.9): sin fuentes propias.

                // Servicios optimizados para .NET 9 - MAUI Nativo
                builder.Services.AddSingleton<SettingsService>();
                builder.Services.AddSingleton<TranslationService>();
                
                // Servicios de app (Singleton): antes eran Scoped y al resolverlos a mano desde
                // Handler.MauiContext.Services en las paginas fallaba/daba instancias distintas, y
                // la geolocalizacion no llegaba a arrancar. Como servicios de aplicacion, Singleton.
                builder.Services.AddSingleton<GeolocationService>();
                builder.Services.AddSingleton<FallbackGeolocationService>();

                builder.Services.AddSingleton<RouteService>();
                builder.Services.AddSingleton<GpsFilterService>();

                // Ajuste de la ruta contra el mapa. El tiempo de espera es generoso porque
                // Overpass tarda: es una consulta unica al terminar de grabar, no algo continuo.
                builder.Services.AddSingleton(new HttpClient { Timeout = TimeSpan.FromSeconds(30) });
                builder.Services.AddSingleton<MapMatchService>();

                // La grabacion de la ruta es un servicio de aplicacion, no estado de una pagina:
                // quien le entrega los puntos es el servicio en primer plano, que sigue vivo con la
                // pantalla apagada, y cada punto se escribe en disco nada mas llegar.
                builder.Services.AddSingleton<TrackRecorder>();

                // Comprobacion de version al arrancar (constitucion seccion 15).
                builder.Services.AddSingleton<UpdateService>();

                // Servicios de CommunityToolkit
                try
                {
                    builder.Services.AddSingleton<IFileSaver>(FileSaver.Default);
                }
                catch (Exception fileSaverEx)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"IFileSaver registration failed, using MockFileSaver fallback: {fileSaverEx}");
                    builder.Services.AddSingleton<IFileSaver>(provider => new MockFileSaver());
                }

                // Registrar páginas para navegación
                builder.Services.AddTransient<Pages.HomePage>();
                builder.Services.AddTransient<Pages.RoutesPage>();
                builder.Services.AddTransient<Pages.SettingsPage>();
                builder.Services.AddTransient<Pages.AboutPage>();

#if DEBUG
                builder.Logging.AddDebug();
#endif

                return builder.Build();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing MAUI app: {ex.Message}");
                throw;
            }
        }
    }

    // Mock FileSaver para casos donde no esté disponible
    public class MockFileSaver : IFileSaver
    {
        public Task<FileSaverResult> SaveAsync(string fileName, Stream stream, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new FileSaverResult("", new Exception("FileSaver not available")));
        }

        public Task<FileSaverResult> SaveAsync(string defaultFileName, Stream stream, string contentType, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new FileSaverResult("", new Exception("FileSaver not available")));
        }

        public Task<FileSaverResult> SaveAsync(string fileName, string filePath, Stream stream, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new FileSaverResult("", new Exception("FileSaver not available")));
        }

        public Task<FileSaverResult> SaveAsync(string fileName, string filePath, Stream stream, IProgress<double> progress, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new FileSaverResult("", new Exception("FileSaver not available")));
        }

        public Task<FileSaverResult> SaveAsync(string fileName, Stream stream, IProgress<double> progress, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new FileSaverResult("", new Exception("FileSaver not available")));
        }
    }
}
