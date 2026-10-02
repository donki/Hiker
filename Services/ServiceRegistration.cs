using Microsoft.Extensions.DependencyInjection;

namespace Hiker.Services;

/// <summary>
/// Los servicios de la app, todos Singleton: antes eran Scoped y al resolverlos a mano desde
/// Handler.MauiContext.Services en las paginas fallaba/daba instancias distintas, y la
/// geolocalizacion no llegaba a arrancar. Lo propio de cada plataforma (servicio en primer plano,
/// bateria, enlaces) lo registra MauiProgram.
/// </summary>
public static class ServiceRegistration
{
    public static IServiceCollection AddHikerServices(this IServiceCollection services)
    {
        services.AddSingleton<SettingsService>();
        services.AddSingleton<TranslationService>();

        // Lo que la logica usa del movil, por interfaz (ver PlatformContracts.cs).
        services.AddSingleton(Geolocation.Default);
        services.AddSingleton(Compass.Default);
        services.AddSingleton(Microsoft.Maui.Storage.Preferences.Default);
        services.AddSingleton<ILocationPermission, EssentialsLocationPermission>();

        services.AddSingleton<GeolocationService>();
        services.AddSingleton<ILocationSource>(sp => sp.GetRequiredService<GeolocationService>());

        services.AddSingleton<RouteService>();
        services.AddSingleton<GpsFilterService>();

        // Ajuste de la ruta contra el mapa. El tiempo de espera es generoso porque Overpass tarda:
        // es una consulta unica al terminar de grabar, no algo continuo.
        services.AddSingleton(new HttpClient { Timeout = TimeSpan.FromSeconds(30) });
        services.AddSingleton<MapMatchService>();

        // La grabacion es un servicio de aplicacion, no estado de una pagina: quien le entrega los
        // puntos es el servicio en primer plano, que sigue vivo con la pantalla apagada.
        services.AddSingleton<TrackRecorder>();

        // Comprobacion de version al arrancar (constitucion seccion 15).
        services.AddSingleton<UpdateService>(sp => new UpdateService(
            sp.GetRequiredService<TranslationService>(), sp.GetRequiredService<ILinkOpener>()));

        return services;
    }
}
