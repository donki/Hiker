using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;

namespace Hiker;

/// <summary>
/// Servicio en primer plano que mantiene viva la grabacion de la ruta.
/// </summary>
/// <remarks>
/// No lee el GPS: de eso sigue encargandose <see cref="Services.GeolocationService"/> desde la
/// aplicacion. Lo que aporta este servicio es que el proceso no se congele cuando la pantalla se
/// apaga o el usuario cambia de aplicacion, que es lo normal en una ruta de tres horas: sin el,
/// Android deja de entregar posiciones a los pocos minutos y la grabacion queda a trozos.
///
/// Al declararse de tipo <c>location</c> y arrancarse con la aplicacion en pantalla, Android
/// permite seguir recibiendo posiciones sin pedir <c>ACCESS_BACKGROUND_LOCATION</c>, que es un
/// permiso restringido y obligaria a declaracion y video de demostracion en Play.
///
/// La notificacion no es decorativa: es el requisito de Android para poder hacer esto, y ademas
/// deja claro al usuario que Hiker esta grabando.
/// </remarks>
[Service(Exported = false, ForegroundServiceType = global::Android.Content.PM.ForegroundService.TypeLocation)]
public class TrackingForegroundService : Service
{
    private const string ChannelId = "hiker_tracking";
    private const int NotificationId = 8801;

    /// <summary>Texto de la notificacion. Lo pone la capa MAUI, que es la que sabe de idiomas.</summary>
    public static string NotificationTitle { get; set; } = "Hiker";

    public static string NotificationText { get; set; } = "Grabando la ruta";

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

        // Sticky: si Android mata el proceso por memoria, la grabacion se reanuda al recrearlo.
        return StartCommandResult.Sticky;
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
