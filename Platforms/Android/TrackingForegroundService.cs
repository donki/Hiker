using Android.App;
using Android.Content;
using Android.Locations;
using Android.OS;
using AndroidX.Core.App;
using Hiker.Services;
using AndroidLocation = Android.Locations.Location;
using MauiLocation = Microsoft.Maui.Devices.Sensors.Location;

namespace Hiker;

/// <summary>
/// Servicio en primer plano que graba la ruta: escucha al GPS y entrega cada posicion a
/// <see cref="TrackRecorder"/>.
/// </summary>
/// <remarks>
/// Antes este servicio no leia el GPS — solo mantenia vivo el proceso — y quien escuchaba era la
/// pagina del mapa a traves de MAUI Essentials. Con la pantalla apagada eso no aguanta: la pagina
/// puede irse, la precision «media» se apoya en proveedores que Android suspende en reposo, y los
/// puntos solo existian en memoria hasta que el usuario pulsaba guardar. Resultado: rutas a trozos
/// o directamente vacias.
///
/// Ahora la escucha vive aqui, pegada al servicio que Android mantiene vivo: se pide el proveedor
/// **GPS** (el unico fiable en marcha y el que sigue entregando en reposo con un servicio de tipo
/// <c>location</c>), se coge un bloqueo parcial de CPU para poder procesar cada fix con la pantalla
/// apagada, y cada punto se escribe en el diario nada mas llegar.
///
/// Al declararse de tipo <c>location</c> y arrancarse con la aplicacion en pantalla, Android permite
/// seguir recibiendo posiciones sin pedir <c>ACCESS_BACKGROUND_LOCATION</c>, que es un permiso
/// restringido y obligaria a declaracion y video de demostracion en Play.
/// </remarks>
[Service(Exported = false, ForegroundServiceType = global::Android.Content.PM.ForegroundService.TypeLocation)]
public class TrackingForegroundService : Service, ILocationListener
{
    private const string ChannelId = "hiker_tracking";
    private const int NotificationId = 8801;

    /// <summary>Distancia minima entre entregas. A cero: filtrar es cosa de <see cref="GpsFilterService"/>.</summary>
    private const float MinimumDistanceMeters = 0f;

    /// <summary>Texto de la notificacion. Lo pone la capa MAUI, que es la que sabe de idiomas.</summary>
    public static string NotificationTitle { get; set; } = "Hiker";

    public static string NotificationText { get; set; } = "Grabando la ruta";

    private LocationManager? _locationManager;
    private PowerManager.WakeLock? _wakeLock;
    private bool _listening;

    public static void Start()
    {
        var context = global::Android.App.Application.Context;
        var intent = new Intent(context, typeof(TrackingForegroundService));

        if (OperatingSystem.IsAndroidVersionAtLeast(26))
            context.StartForegroundService(intent);
        else
            context.StartService(intent);
    }

    public static void Stop()
    {
        var context = global::Android.App.Application.Context;
        context.StopService(new Intent(context, typeof(TrackingForegroundService)));
    }

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        CreateChannel();
        StartInForeground();
        AcquireWakeLock();

        // Si Android habia matado el proceso a mitad de ruta, el servicio se recrea solo (Sticky)
        // pero el grabador nace vacio: se reanuda desde el diario para no partir la traza en dos.
        var recorder = IPlatformApplication.Current?.Services.GetService<TrackRecorder>();
        if (recorder?.ResumeIfInterrupted() == true)
            GeolocationService.LogInfo($"Grabacion reanudada tras reinicio del servicio: {recorder.PointCount} puntos.");

        StartListening();

        // Sticky: si Android mata el proceso por memoria, el servicio se recrea y vuelve a
        // escuchar. Lo grabado hasta entonces no se pierde porque esta en el diario en disco.
        return StartCommandResult.Sticky;
    }

    public override void OnDestroy()
    {
        StopListening();
        ReleaseWakeLock();
        base.OnDestroy();
    }

    // ==================================================================================
    //  Escucha del GPS
    // ==================================================================================

    private void StartListening()
    {
        if (_listening)
            return;

        _locationManager = (LocationManager?)GetSystemService(LocationService);
        if (_locationManager is null)
            return;

        var intervalMs = ReadIntervalMilliseconds();

        try
        {
            // GPS es el proveedor que hay que usar para grabar una ruta: es el unico que da
            // precision de metros en marcha y el que sigue entregando con la pantalla apagada
            // mientras corra este servicio.
            if (_locationManager.IsProviderEnabled(LocationManager.GpsProvider))
            {
                _locationManager.RequestLocationUpdates(
                    LocationManager.GpsProvider, intervalMs, MinimumDistanceMeters, this);
                _listening = true;
            }

            // Red como apoyo: en un barranco o dentro de un edificio el GPS no fija, y mas vale un
            // punto de cien metros que un hueco en la traza. El filtro descarta los que no valgan.
            if (_locationManager.IsProviderEnabled(LocationManager.NetworkProvider))
            {
                _locationManager.RequestLocationUpdates(
                    LocationManager.NetworkProvider, intervalMs, MinimumDistanceMeters, this);
                _listening = true;
            }
        }
        catch (Java.Lang.SecurityException ex)
        {
            // Sin permiso de ubicacion no hay nada que escuchar; la pagina ya lo pide al arrancar.
            GeolocationService.LogInfo($"TrackingForegroundService sin permiso de ubicacion: {ex.Message}");
        }
    }

    private void StopListening()
    {
        if (!_listening)
            return;

        try
        {
            _locationManager?.RemoveUpdates(this);
        }
        catch (Java.Lang.SecurityException)
        {
            // Permiso revocado mientras se grababa: no hay nada que quitar.
        }

        _listening = false;
    }

    /// <summary>Intervalo elegido por el usuario en Configuracion, con un minimo de un segundo.</summary>
    private static long ReadIntervalMilliseconds()
    {
        var settings = IPlatformApplication.Current?.Services.GetService<SettingsService>();
        var seconds = settings?.AppSettings.TimerInterval ?? 1;
        return Math.Max(1000, seconds * 1000);
    }

    public void OnLocationChanged(AndroidLocation location)
    {
        var recorder = IPlatformApplication.Current?.Services.GetService<TrackRecorder>();
        if (recorder is null || !recorder.IsRecording)
            return;

        var point = new MauiLocation(location.Latitude, location.Longitude,
            location.HasAltitude ? location.Altitude : 0)
        {
            Accuracy = location.HasAccuracy ? location.Accuracy : null,
            Speed = location.HasSpeed ? location.Speed : null,
            Course = location.HasBearing ? location.Bearing : null,
            Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(location.Time),
        };

        recorder.Push(point);
    }

    public void OnProviderDisabled(string provider) { }

    public void OnProviderEnabled(string provider) { }

    public void OnStatusChanged(string? provider, [global::Android.Runtime.GeneratedEnum] Availability status, Bundle? extras) { }

    // ==================================================================================
    //  Notificacion y CPU
    // ==================================================================================

    /// <summary>
    /// Bloqueo parcial de CPU: el servicio en primer plano evita que Android congele el proceso,
    /// pero con la pantalla apagada la CPU se duerme entre fix y fix y los puntos llegan a rachas.
    /// Se libera al parar de grabar.
    /// </summary>
    private void AcquireWakeLock()
    {
        if (_wakeLock is not null)
            return;

        var power = (PowerManager?)GetSystemService(PowerService);
        _wakeLock = power?.NewWakeLock(WakeLockFlags.Partial, "Hiker:tracking");
        _wakeLock?.Acquire();
    }

    private void ReleaseWakeLock()
    {
        if (_wakeLock is { IsHeld: true })
            _wakeLock.Release();

        _wakeLock?.Dispose();
        _wakeLock = null;
    }

    private void CreateChannel()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26))
            return;

        var manager = (NotificationManager?)GetSystemService(NotificationService);
        if (manager is null)
            return;

        // Importancia baja: informa de que se esta grabando, pero no suena ni interrumpe.
        var channel = new NotificationChannel(ChannelId, "Hiker", NotificationImportance.Low);
        manager.CreateNotificationChannel(channel);
    }

    private void StartInForeground()
    {
        var launch = PackageManager?.GetLaunchIntentForPackage(PackageName!);
        var pending = launch is null
            ? null
            : PendingIntent.GetActivity(this, 0, launch,
                OperatingSystem.IsAndroidVersionAtLeast(23)
                    ? PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent
                    : PendingIntentFlags.UpdateCurrent);

        var notification = new NotificationCompat.Builder(this, ChannelId)
            .SetContentTitle(NotificationTitle)
            .SetContentText(NotificationText)
            .SetSmallIcon(Resource.Drawable.ic_notification)
            .SetOngoing(true)
            .SetContentIntent(pending)
            .SetPriority((int)NotificationPriority.Low)
            .Build();

        if (OperatingSystem.IsAndroidVersionAtLeast(29))
            StartForeground(NotificationId, notification, global::Android.Content.PM.ForegroundService.TypeLocation);
        else
            StartForeground(NotificationId, notification);
    }
}
