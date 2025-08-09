namespace Hiker
{
    public partial class MainPage : ContentPage
    {
        public MainPage()
        {
            InitializeComponent();

            // Llamamos a la función para mostrar el BlazorWebView después de 3 segundos
            ShowBlazorWebViewAfterDelay();
        }

        // Método para esperar 3 segundos y luego mostrar el BlazorWebView con animación
        private async void ShowBlazorWebViewAfterDelay()
        {
            // Esperar 3 segundos (3000 milisegundos)
            await Task.Delay(3000);

            // Animar la salida del splash screen
            await splashScreen.FadeTo(0, 500, Easing.CubicOut);
            
            // Ocultar el splash screen
            splashScreen.IsVisible = false;

            // Mostrar el BlazorWebView con animación
            blazorWebView.Opacity = 0;
            blazorWebView.IsVisible = true;
            await blazorWebView.FadeTo(1, 500, Easing.CubicIn);
        }
    }
}
