using CommunityToolkit.Maui;
using Hiker.Services;
using Microsoft.Extensions.Logging;

namespace Hiker
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            // Gestor global de excepciones (constitucion General 6.12): un error inesperado se
            // registra y se avisa en el idioma elegido en la app, sin cerrarla (ni cortar una
            // grabacion en curso).
            SocShared.CrashGuard.Install("Hiker", language: () =>
                IPlatformApplication.Current?.Services.GetService<SettingsService>()?.GetSetting("Language"));

            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit();
            // Tipografia del sistema (constitucion A.9): sin fuentes propias.

            builder.Services.AddHikerServices();
            builder.Services.AddSingleton<ILinkOpener, Pages.MauiLinkOpener>();
#if ANDROID
            builder.Services.AddSingleton<ITrackingService, AndroidTrackingService>();
            builder.Services.AddSingleton<IBatterySettings, AndroidBatterySettings>();
#else
            builder.Services.AddSingleton<ITrackingService, NoTrackingService>();
#endif

            builder.Services.AddTransient<Pages.HomePage>();
            builder.Services.AddTransient<Pages.RoutesPage>();
            builder.Services.AddTransient<Pages.SettingsPage>();
            builder.Services.AddTransient<Pages.AboutPage>();

#if DEBUG
            builder.Logging.AddDebug();
#endif
            return builder.Build();
        }
    }
}
