using Microsoft.UI.Xaml;
using System.Runtime.InteropServices;
using Windows.ApplicationModel;

namespace Hiker.Platforms.Windows
{
    public static class WindowsInitializationService
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetProcessDPIAware();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetProcessDpiAwarenessContext(IntPtr value);

        [DllImport("ole32.dll")]
        private static extern int CoInitializeEx(IntPtr pvReserved, uint dwCoInit);

        [DllImport("ole32.dll")]
        private static extern void CoUninitialize();

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeLibrary(IntPtr hModule);

        private const uint COINIT_APARTMENTTHREADED = 0x2;
        private const uint COINIT_MULTITHREADED = 0x0;
        private const uint COINIT_DISABLE_OLE1DDE = 0x4;
        private static readonly IntPtr DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = new IntPtr(-4);

        private static IntPtr _winrtModule = IntPtr.Zero;

        public static void Initialize()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("Starting Windows initialization...");

                // 1. Configurar DPI awareness primero
                ConfigureDpiAwareness();

                // 2. Precargar bibliotecas WinRT necesarias
                PreloadWinRTLibraries();

                // 3. Inicializar COM con configuración robusta
                InitializeCOM();

                // 4. Configurar contexto de sincronización
                ConfigureSynchronizationContext();

                // 5. Inicializar WinRT de forma segura
                InitializeWinRT();

                System.Diagnostics.Debug.WriteLine("Windows initialization completed successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Windows initialization error: {ex.Message}");
                // No lanzar excepción para evitar crash de la app
            }
        }

        private static void ConfigureDpiAwareness()
        {
            try
            {
                // Intentar configuración moderna primero
                if (!SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2))
                {
                    // Fallback a método legacy
                    SetProcessDPIAware();
                }
                System.Diagnostics.Debug.WriteLine("DPI awareness configured");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DPI awareness failed: {ex.Message}");
            }
        }

        private static void PreloadWinRTLibraries()
        {
            try
            {
                // Precargar bibliotecas WinRT críticas
                var libraries = new[]
                {
                    "api-ms-win-core-winrt-l1-1-0.dll",
                    "api-ms-win-core-winrt-string-l1-1-0.dll",
                    "Windows.ApplicationModel.dll"
                };

                foreach (var lib in libraries)
                {
                    try
                    {
                        var handle = LoadLibrary(lib);
                        if (handle != IntPtr.Zero)
                        {
                            System.Diagnostics.Debug.WriteLine($"Loaded library: {lib}");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to load {lib}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Library preloading failed: {ex.Message}");
            }
        }

        private static void InitializeCOM()
        {
            try
            {
                // Intentar diferentes configuraciones COM
                var configurations = new[]
                {
                    COINIT_APARTMENTTHREADED | COINIT_DISABLE_OLE1DDE,
                    COINIT_APARTMENTTHREADED,
                    COINIT_MULTITHREADED | COINIT_DISABLE_OLE1DDE
                };

                foreach (var config in configurations)
                {
                    var hr = CoInitializeEx(IntPtr.Zero, config);
                    if (hr >= 0 || hr == -2147417850) // S_OK o RPC_E_CHANGED_MODE
                    {
                        System.Diagnostics.Debug.WriteLine($"COM initialized with config: 0x{config:X}");
                        return;
                    }
                }

                System.Diagnostics.Debug.WriteLine("All COM initialization attempts failed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"COM initialization failed: {ex.Message}");
            }
        }

        private static void ConfigureSynchronizationContext()
        {
            try
            {
                if (SynchronizationContext.Current == null)
                {
                    SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
                    System.Diagnostics.Debug.WriteLine("Synchronization context configured");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Synchronization context failed: {ex.Message}");
            }
        }

        private static void InitializeWinRT()
        {
            try
            {
                // Intentar acceder a una API WinRT simple para verificar funcionamiento
                var packageId = Package.Current?.Id?.Name;
                System.Diagnostics.Debug.WriteLine($"WinRT test successful - Package: {packageId ?? "Unknown"}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WinRT initialization test failed: {ex.Message}");
                
                // Intentar reinicialización COM como último recurso
                try
                {
                    CoUninitialize();
                    Thread.Sleep(100);
                    var hr = CoInitializeEx(IntPtr.Zero, COINIT_APARTMENTTHREADED);
                    System.Diagnostics.Debug.WriteLine($"COM reinitialization result: 0x{hr:X}");
                }
                catch (Exception reinitEx)
                {
                    System.Diagnostics.Debug.WriteLine($"COM reinitialization failed: {reinitEx.Message}");
                }
            }
        }

        public static void Cleanup()
        {
            try
            {
                // Liberar bibliotecas cargadas
                if (_winrtModule != IntPtr.Zero)
                {
                    FreeLibrary(_winrtModule);
                    _winrtModule = IntPtr.Zero;
                }

                // Cleanup COM
                CoUninitialize();
                System.Diagnostics.Debug.WriteLine("Windows cleanup completed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Windows cleanup error: {ex.Message}");
            }
        }
    }
}