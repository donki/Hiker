namespace Hiker.WinUI
{
    /// <summary>
    /// Arranque en Windows (objetivo secundario, solo de desarrollo). Antes pasaba por un
    /// WindowsInitializationService que tocaba DPI, COM y precargaba bibliotecas WinRT a mano: nada
    /// de eso hace falta con MAUI, que ya lo resuelve, y se quito el 2026-10-01.
    /// </summary>
    public partial class App : MauiWinUIApplication
    {
        public App() => InitializeComponent();

        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
    }
}
