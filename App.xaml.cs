namespace Hiker
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var window = new Window(new AppShell())
            {
                Title = "Hiker - Tu compañero de aventuras"
            };
#if DEBUG
            SocShared.AuthorNotes.Attach(window);   // notas de autor: SOLO Debug, desactivado en Release/produccion
#endif

            // Rehidratar los ajustes guardados al arrancar. Sin esto, AppSettings siempre volvía a
            // los valores por defecto tras reiniciar (Preferences se ignoraba) y "Guardar"/"Restablecer"
            // parecían no funcionar.
            var settings = IPlatformApplication.Current?.Services
                               ?.GetService(typeof(Hiker.Services.SettingsService)) as Hiker.Services.SettingsService;
            if (settings is not null)
                _ = settings.LoadSettingsAsync();

            return window;
        }
    }
}
