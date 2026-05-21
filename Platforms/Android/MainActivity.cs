using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Util;
using Android.Views;
using Hiker.Services;

namespace Hiker
{
    [Activity(Theme = "@style/AppTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        private TranslationService _translationService;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            _translationService = MauiApplication.Current.Services.GetService<TranslationService>();

            base.OnCreate(savedInstanceState);

            Window.DecorView.SystemUiVisibility = (StatusBarVisibility)SystemUiFlags.Fullscreen;
            SupportActionBar?.Hide();

            CheckAndRequestBatteryOptimization();
            CheckAndRequestBackgroundExecution();
        }

        private void CheckAndRequestBatteryOptimization()
        {
            try
            {
                PowerManager pm = (PowerManager)GetSystemService(PowerService);
                if (pm == null) return;

                if (!pm.IsIgnoringBatteryOptimizations(PackageName))
                {
                    RunOnUiThread(() =>
                    {
                        ShowDialog(
                            _translationService.Translate("Optimización de batería"),
                            _translationService.Translate("Para mejorar el posicionamiento y el rendimiento de Hiker, permite que la aplicación funcione sin restricciones de batería. ¿Deseas modificar esta configuración ahora?"),
                            Android.Provider.Settings.ActionIgnoreBatteryOptimizationSettings
                        );
                    });
                }
            }
            catch (Exception ex)
            {
                Log.Error("Hiker", $"Error al solicitar optimización de batería: {ex.Message}");
            }
        }

        private void CheckAndRequestBackgroundExecution()
        {
            try
            {
                PowerManager pm = (PowerManager)GetSystemService(PowerService);
                if (pm == null) return;

                if (!pm.IsIgnoringBatteryOptimizations(PackageName))
                {
                    RunOnUiThread(() =>
                    {
                        ShowDialog(
                            _translationService.Translate("Ejecución en segundo plano"),
                            _translationService.Translate("Para que Hiker funcione correctamente en segundo plano, permite la ejecución sin restricciones. ¿Deseas modificar esta configuración ahora?"),
                            Android.Provider.Settings.ActionBatterySaverSettings
                        );
                    });
                }
            }
            catch (Exception ex)
            {
                Log.Error("Hiker", $"Error al solicitar permisos de segundo plano: {ex.Message}");
            }
        }

        private void ShowDialog(string title, string message, string settingsAction)
        {
            string yesButton = _translationService.Translate("Sí");
            string noButton = _translationService.Translate("No");

            new AlertDialog.Builder(this)
                .SetTitle(title)
                .SetMessage(message)
                .SetCancelable(false)
                .SetPositiveButton(yesButton, (sender, e) =>
                {
                    try
                    {
                        Intent intent = new Intent(settingsAction);
                        StartActivity(intent);
                    }
                    catch (Exception ex)
                    {
                        Log.Error("Hiker", $"Error al abrir configuración: {ex.Message}");
                    }
                })
                .SetNegativeButton(noButton, (sender, e) => { /* No hacer nada */ })
                .Show();
        }
    }
}
