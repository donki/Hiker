using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
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
            ShowEnergySettingsDialog();
            ShowBackgroundSettingsDialog();

        }

        public void ShowEnergySettingsDialog()
        {

            string title = _translationService.Translate("Optimización de batería");
            string message = _translationService.Translate("Para mejorar el posicionamiento, debes configurar Hiker para que no tenga restricciones de batería. ¿Quieres cambiar la configuración?");
            string yesButton = _translationService.Translate("Sí");
            string noButton = _translationService.Translate("No");

            // Verificar si el ahorro de energía está habilitado
            PowerManager pm = (PowerManager)GetSystemService(PowerService);
            if (!pm.IsIgnoringBatteryOptimizations(PackageName))
            {
                // Mostrar un cuadro de diálogo para que el usuario desactive el ahorro de energía
                new AlertDialog.Builder(this)
                    .SetTitle(title)
                    .SetMessage(message)
                    .SetPositiveButton(yesButton, (sender, e) =>
                    {

                        Intent intent = new Intent(Android.Provider.Settings.ActionIgnoreBatteryOptimizationSettings);
                        StartActivity(intent);

                    })
                    .SetNegativeButton(noButton, (sender, e) => { /* No hacer nada */ })
                    .Show();
            }
        }

        public void ShowBackgroundSettingsDialog()
        {
            string title = _translationService.Translate("Ejecución en segundo plano");
            string message = _translationService.Translate("Para que Hiker funcione correctamente en segundo plano, debes permitir que no tenga restricciones de ejecución. ¿Quieres cambiar la configuración?");
            string yesButton = _translationService.Translate("Sí");
            string noButton = _translationService.Translate("No");
            PowerManager pm = (PowerManager)GetSystemService(PowerService);
            if (!pm.IsIgnoringBatteryOptimizations(PackageName))
            {
                new AlertDialog.Builder(this)
                    .SetTitle(title)
                    .SetMessage(message)
                    .SetPositiveButton(yesButton, (sender, e) =>
                    {
                        // Intent para acceder a la configuración de optimización de batería
                        Intent intent = new Intent(Android.Provider.Settings.ActionBatterySaverSettings);
                        StartActivity(intent);
                    })
                    .SetNegativeButton(noButton, (sender, e) => { /* No hacer nada */ })
                    .Show();
            };
        }
    }
}
