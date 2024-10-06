namespace Hiker
{
    public partial class MainPage : ContentPage
    {
        public MainPage()
        {
            InitializeComponent();

            // Llamamos a la función para mostrar el BlazorWebView después de 5 segundos
            ShowBlazorWebViewAfterDelay();
        }

        // Método para esperar 5 segundos y luego mostrar el BlazorWebView
        private async void ShowBlazorWebViewAfterDelay()
        {
            // Esperar 5 segundos (5000 milisegundos)
            await Task.Delay(5000);

            // Ocultar la imagen del splash
            splashImage.IsVisible = false;

            // Mostrar el BlazorWebView
            blazorWebView.IsVisible = true;
        }
    }
}
