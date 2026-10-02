using Hiker.Services;

namespace Hiker;

/// <summary>El servicio en primer plano que graba con la pantalla apagada.</summary>
public sealed class AndroidTrackingService : ITrackingService
{
    public void Start(string title, string text)
    {
        TrackingForegroundService.NotificationTitle = title;
        TrackingForegroundService.NotificationText = text;
        TrackingForegroundService.Start();
    }

    public void Stop() => TrackingForegroundService.Stop();
}

/// <summary>Los ajustes de bateria del sistema (los abre MainActivity).</summary>
public sealed class AndroidBatterySettings : IBatterySettings
{
    public bool IsOptimizationIgnored() => MainActivity.IsBatteryOptimizationIgnored();
    public void OpenOptimizationSettings() => MainActivity.OpenBatteryOptimizationSettings();
    public void OpenBatterySaverSettings() => MainActivity.OpenBatterySaverSettings();
}
