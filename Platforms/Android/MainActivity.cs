using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
namespace Hiker
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            ShowEnergySettingsDialog();

        }

        public void ShowEnergySettingsDialog()
        {
            // Verificar si el ahorro de energía está habilitado
            PowerManager pm = (PowerManager)GetSystemService(PowerService);
            if (!pm.IsIgnoringBatteryOptimizations(PackageName))
            {
                // Mostrar un cuadro de diálogo para que el usuario desactive el ahorro de energía
                new AlertDialog.Builder(this)
                    .SetTitle("Optimización de batería")
                    .SetMessage("Para mejorar el posicionamiento, debes configurar Hiker para que no tenga restricciones de batería. ¿Quieres cambiar la configuración?")
                    .SetPositiveButton("Sí", (sender, e) =>
                    {

                        Intent intent = new Intent(Android.Provider.Settings.ActionIgnoreBatteryOptimizationSettings);
                        StartActivity(intent);

                    })
                    .SetNegativeButton("No", (sender, e) => { /* No hacer nada */ })
                    .Show();
            }
        }
    }
}
