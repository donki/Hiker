namespace Hiker.Services;

// Lo que la logica de Hiker necesita del movil, en interfaces: la app usa las implementaciones
// reales (MAUI Essentials, Android) y las pruebas, dobles. Asi la logica se puede probar sin
// dispositivo (constitucion General 8.6).

/// <summary>Ubicacion en vivo para el mapa.</summary>
public interface ILocationSource
{
    event Action<Location>? LocationChanged;
    Task<bool> ListeningStartAsync();
    Task ListeningStopAsync();
    Task<Location?> GetCurrentLocationAsync();
    Task<Location?> GetLastKnownLocationAsync();
}

/// <summary>Permiso de ubicacion mientras se usa la app.</summary>
public interface ILocationPermission
{
    Task<PermissionStatus> CheckAsync();
    Task<PermissionStatus> RequestAsync();
}

/// <summary>Servicio en primer plano que graba con la pantalla apagada (solo Android).</summary>
public interface ITrackingService
{
    void Start(string title, string text);
    void Stop();
}

/// <summary>Ajustes de bateria del sistema (solo Android).</summary>
public interface IBatterySettings
{
    bool IsOptimizationIgnored();
    void OpenOptimizationSettings();
    void OpenBatterySaverSettings();
}

/// <summary>Hilo de la interfaz: lo que llega de otros hilos (GPS, brujula) se pinta aqui.</summary>
public interface IUiThread
{
    void Post(Func<Task> action);
}

/// <summary>Dialogos de la app (ModernDialog en la app real).</summary>
public interface IUserDialogs
{
    Task AlertAsync(string title, string message, string ok);
    Task<bool> ConfirmAsync(string title, string message, string accept, string cancel);
    Task<string?> PromptAsync(string title, string message, string accept, string cancel);
}

/// <summary>Navegacion entre pantallas.</summary>
public interface INavigator
{
    Task GoToAsync(string route);
    Task ShowRouteInfoAsync(string routeName);
}

/// <summary>Elegir un fichero GPX: devuelve su nombre y su contenido, o null si se cancela.</summary>
public interface IGpxPicker
{
    Task<(string FileName, string Content)?> PickAsync(string title);
}

/// <summary>Abrir enlaces y el correo.</summary>
public interface ILinkOpener
{
    Task OpenAsync(string uri);
}

/// <summary>Permiso de ubicacion real, con MAUI Essentials.</summary>
public sealed class EssentialsLocationPermission : ILocationPermission
{
    public Task<PermissionStatus> CheckAsync() => Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
    public Task<PermissionStatus> RequestAsync() => Permissions.RequestAsync<Permissions.LocationWhenInUse>();
}

/// <summary>Donde no hay servicio en primer plano (Windows): grabar sigue funcionando mientras la app este abierta.</summary>
public sealed class NoTrackingService : ITrackingService
{
    public void Start(string title, string text) { }
    public void Stop() { }
}
