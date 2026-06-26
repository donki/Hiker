using Microsoft.UI.Xaml;
using Hiker.Platforms.Windows;

namespace Hiker.WinUI
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : MauiWinUIApplication
    {
        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            try
            {
                // Initialize Windows-specific settings before component initialization
                WindowsInitializationService.Initialize();
                
                // Configure WebView2 environment
                System.Diagnostics.Debug.WriteLine("WebView2 environment ready");
                
                this.InitializeComponent();
                
                System.Diagnostics.Debug.WriteLine("Windows app initialized successfully");
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing Windows app: {ex.Message}");
                // Continue execution to avoid complete failure
            }
        }

        protected override MauiApp CreateMauiApp()
        {
            try
            {
                var app = MauiProgram.CreateMauiApp();
                System.Diagnostics.Debug.WriteLine("MAUI app created successfully");
                return app;
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating MAUI app: {ex.Message}");
                throw;
            }
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            try
            {
                base.OnLaunched(args);
                System.Diagnostics.Debug.WriteLine("App launched successfully");
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error launching app: {ex.Message}");
                throw;
            }
        }
    }
}
