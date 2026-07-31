using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Util;
using Android.Views;
using AndroidX.Core.View;
using AndroidView = Android.Views.View;

namespace Hiker
{
    [Activity(Theme = "@style/AppTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            // Antes se forzaba pantalla completa (SystemUiFlags.Fullscreen), que metía el contenido
            // bajo el reloj y la barra de botones. En su lugar se separan las barras con insets.
            SupportActionBar?.Hide();
            ApplySystemBarInsets();

            // Los avisos de bateria/segundo plano ya NO se muestran con AlertDialog nativo
            // (prohibido por la constitucion). Ahora se piden desde la capa MAUI
            // (HomePage.OnAppearing) con SocShared.ModernDialog, que invoca estos ayudantes.
        }

        // ---- Ayudantes de bateria/segundo plano llamados desde la capa MAUI ----

        /// <summary>Indica si la app esta exenta de las optimizaciones de bateria.</summary>
        public static bool IsBatteryOptimizationIgnored()
        {
            try
            {
                var context = Android.App.Application.Context;
                var pm = (PowerManager?)context.GetSystemService(PowerService);
                if (pm == null) return true; // sin PowerManager no molestamos al usuario
                return pm.IsIgnoringBatteryOptimizations(context.PackageName);
            }
            catch (Exception ex)
            {
                Log.Error("Hiker", $"Error comprobando optimización de batería: {ex.Message}");
                return true;
            }
        }

        /// <summary>Abre los ajustes del sistema para ignorar la optimización de batería.</summary>
        public static void OpenBatteryOptimizationSettings() =>
            LaunchSettings(Android.Provider.Settings.ActionIgnoreBatteryOptimizationSettings);

        /// <summary>Abre los ajustes del ahorro de batería (ejecución en segundo plano).</summary>
        public static void OpenBatterySaverSettings() =>
            LaunchSettings(Android.Provider.Settings.ActionBatterySaverSettings);

        private static void LaunchSettings(string settingsAction)
        {
            try
            {
                var intent = new Intent(settingsAction);
                var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
                if (activity != null)
                {
                    activity.StartActivity(intent);
                }
                else
                {
                    intent.AddFlags(ActivityFlags.NewTask);
                    Android.App.Application.Context.StartActivity(intent);
                }
            }
            catch (Exception ex)
            {
                Log.Error("Hiker", $"Error al abrir configuración: {ex.Message}");
            }
        }

        // Android 15 dibuja de borde a borde: separa el contenido del reloj y de la barra inferior.
        private void ApplySystemBarInsets()
        {
            var content = FindViewById(global::Android.Resource.Id.Content);
            if (content is null) return;
            content.SetBackgroundColor(global::Android.Graphics.Color.ParseColor("#2A1CB8")); // indigo de marca
            ViewCompat.SetOnApplyWindowInsetsListener(content, new SystemBarInsetsListener());
            var controller = Window is not null ? WindowCompat.GetInsetsController(Window, Window.DecorView) : null;
            if (controller is not null)
            {
                controller.AppearanceLightStatusBars = false;
                controller.AppearanceLightNavigationBars = false;
            }
        }

        private class SystemBarInsetsListener : Java.Lang.Object, IOnApplyWindowInsetsListener
        {
            public WindowInsetsCompat OnApplyWindowInsets(AndroidView? view, WindowInsetsCompat? insets)
            {
                var consumed = WindowInsetsCompat.Consumed!;
                if (view is null || insets is null) return consumed;
                var bars = insets.GetInsets(WindowInsetsCompat.Type.SystemBars() | WindowInsetsCompat.Type.DisplayCutout());
                if (bars is not null) view.SetPadding(bars.Left, bars.Top, bars.Right, bars.Bottom);
                return consumed;
            }
        }
    }
}
