namespace Hiker.Services
{
    public static class Translations
    {
        // Diccionario que contiene las frases nativas y sus traducciones por idioma
        public static Dictionary<string, Dictionary<string, string>> TranslationDict = new Dictionary<string, Dictionary<string, string>>
        {
            // Frases de Ejecución en segundo plano
            { "Ejecución en segundo plano", new Dictionary<string, string>
                {
                    { "es", "Ejecución en segundo plano" },
                    { "en", "Background Execution" }
                }
            },
            { "Para que Hiker funcione correctamente en segundo plano, debes permitir que no tenga restricciones de ejecución. ¿Quieres cambiar la configuración?", new Dictionary<string, string>
                {
                    { "es", "Para que Hiker funcione correctamente en segundo plano, debes permitir que no tenga restricciones de ejecución. ¿Quieres cambiar la configuración?" },
                    { "en", "For Hiker to function properly in the background, you need to allow it to run without restrictions. Do you want to change the settings?" }
                }
            },
            // Frases de Optimización de batería
            { "Optimización de batería", new Dictionary<string, string>
                {
                    { "es", "Optimización de batería" },
                    { "en", "Battery Optimization" }
                }
            },
            { "Para mejorar el posicionamiento, debes configurar Hiker para que no tenga restricciones de batería. ¿Quieres cambiar la configuración?", new Dictionary<string, string>
                {
                    { "es", "Para mejorar el posicionamiento, debes configurar Hiker para que no tenga restricciones de batería. ¿Quieres cambiar la configuración?" },
                    { "en", "To improve positioning, you need to configure Hiker to have no battery restrictions. Do you want to change the settings?" }
                }
            },
            // Botones Sí y No
            { "Sí", new Dictionary<string, string>
                {
                    { "es", "Sí" },
                    { "en", "Yes" }
                }
            },
            { "No", new Dictionary<string, string>
                {
                    { "es", "No" },
                    { "en", "No" }
                }
            },
            // Frases del componente Acerca de
            { "Acerca de esta aplicación", new Dictionary<string, string>
                {
                    { "es", "Acerca de esta aplicación" },
                    { "en", "About this application" }
                }
            },
            { "Esta aplicación ha sido desarrollada por Organic Coating para ayudar a los usuarios a rastrear y mapear sus rutas.", new Dictionary<string, string>
                {
                    { "es", "Esta aplicación ha sido desarrollada por Organic Coating para ayudar a los usuarios a rastrear y mapear sus rutas." },
                    { "en", "This application has been developed by Organic Coating to help users track and map their routes." }
                }
            },
            { "Créditos", new Dictionary<string, string>
                {
                    { "es", "Créditos" },
                    { "en", "Credits" }
                }
            },
            { "se utilizan bajo sus términos de licencia. Estos iconos proporcionan un aspecto moderno y elegante a la interfaz de usuario de la aplicación.", new Dictionary<string, string>
                {
                    { "es", "se utilizan bajo sus términos de licencia. Estos iconos proporcionan un aspecto moderno y elegante a la interfaz de usuario de la aplicación." },
                    { "en", "are used under their license terms. These icons provide a modern and stylish look to the app's user interface." }
                }
            },
            { "Iconos de", new Dictionary<string, string>
                {
                    { "es", "Iconos de" },
                    { "en", "Icons by" }
                }
            },
            { "también se utilizan en esta aplicación, mejorando su usabilidad y diseño visual.", new Dictionary<string, string>
                {
                    { "es", "también se utilizan en esta aplicación, mejorando su usabilidad y diseño visual." },
                    { "en", "are also used in this app, enhancing its usability and visual design." }
                }
            },
            { "Esta aplicación utiliza datos de mapas proporcionados por", new Dictionary<string, string>
                {
                    { "es", "Esta aplicación utiliza datos de mapas proporcionados por" },
                    { "en", "This app uses map data provided by" }
                }
            },
            { "una fuente libre y abierta de datos de mapas que es mantenida por una comunidad global de colaboradores.", new Dictionary<string, string>
                {
                    { "es", "una fuente libre y abierta de datos de mapas que es mantenida por una comunidad global de colaboradores." },
                    { "en", "a free and open source of map data maintained by a global community of contributors." }
                }
            },
            { "La visualización de los mapas y rutas en esta aplicación se realiza mediante", new Dictionary<string, string>
                {
                    { "es", "La visualización de los mapas y rutas en esta aplicación se realiza mediante" },
                    { "en", "The visualization of maps and routes in this app is done using" }
                }
            },
            { "una biblioteca JavaScript de código abierto que ofrece funcionalidades de mapas interactivas y altamente personalizables.", new Dictionary<string, string>
                {
                    { "es", "una biblioteca JavaScript de código abierto que ofrece funcionalidades de mapas interactivas y altamente personalizables." },
                    { "en", "an open-source JavaScript library that offers interactive and highly customizable mapping functionalities." }
                }
            },
            { "Agradecimientos a Michael Coyle por su librería BlueToque.SharpGPX para la lectura y escritura de archivos GPX.", new Dictionary<string, string>
                {
                    { "es", "Agradecimientos a Michael Coyle por su librería BlueToque.SharpGPX para la lectura y escritura de archivos GPX." },
                    { "en", "Thanks to Michael Coyle for his library BlueToque.SharpGPX for reading and writing GPX files." }
                }
            },
            { "Volver a Inicio", new Dictionary<string, string>
                {
                    { "es", "Volver a Inicio" },
                    { "en", "Return to Home" }
                }
            },            { "Lat:", new Dictionary<string, string>
                {
                    { "es", "Lat:" },
                    { "en", "Lat:" }
                }
            },
            { "Lon:", new Dictionary<string, string>
                {
                    { "es", "Lon:" },
                    { "en", "Lon:" }
                }
            },
            { "Cargar Ruta", new Dictionary<string, string>
                {
                    { "es", "Cargar Ruta" },
                    { "en", "Load Route" }
                }
            },
            { "Modo Ruta", new Dictionary<string, string>
                {
                    { "es", "Modo Ruta" },
                    { "en", "Route Mode" }
                }
            },
            { "Modo Grabación", new Dictionary<string, string>
                {
                    { "es", "Modo Grabación" },
                    { "en", "Recording Mode" }
                }
            },
            { "Configuración", new Dictionary<string, string>
                {
                    { "es", "Configuración" },
                    { "en", "Settings" }
                }
            },
            { "Acerca de...", new Dictionary<string, string>
                {
                    { "es", "Acerca de..." },
                    { "en", "About..." }
                }
            },
            { "Confirmación", new Dictionary<string, string>
                {
                    { "es", "Confirmación" },
                    { "en", "Confirmation" }
                }
            },
            { "¿Desea guardar la ruta grabada?", new Dictionary<string, string>
                {
                    { "es", "¿Desea guardar la ruta grabada?" },
                    { "en", "Do you want to save the recorded route?" }
                }
            },
            { "Estás aquí...", new Dictionary<string, string>
                {
                    { "es", "Estás aquí..." },
                    { "en", "You are here..." }
                }
            },
            { "Grabando...", new Dictionary<string, string>
                {
                    { "es", "Grabando..." },
                    { "en", "Recording..." }
                }
            },
            { "Ruta", new Dictionary<string, string>
                {
                    { "es", "Ruta" },
                    { "en", "Route" }
                }
            },
            { "Distancia Total", new Dictionary<string, string>
                {
                    { "es", "Distancia Total" },
                    { "en", "Total Distance" }
                }
            },
            { "Elevación Mínima", new Dictionary<string, string>
                {
                    { "es", "Elevación Mínima" },
                    { "en", "Minimum Elevation" }
                }
            },
            { "Elevación", new Dictionary<string, string>
                {
                    { "es", "Elevación" },
                    { "en", "Maximum" }
                }
            },            { "Elevación Máxima", new Dictionary<string, string>
                {
                    { "es", "Elevación Máxima" },
                    { "en", "Maximum Elevation" }
                }
            },
            { "Desnivel", new Dictionary<string, string>
                {
                    { "es", "Desnivel" },
                    { "en", "Elevation Gain" }
                }
            },
            { "Tiempo Estimado", new Dictionary<string, string>
                {
                    { "es", "Tiempo Estimado" },
                    { "en", "Estimated Time" }
                }
            },
            { "Al mapa", new Dictionary<string, string>
                {
                    { "es", "Al mapa" },
                    { "en", "To the Map" }
                }
            },
            { "Cerrar", new Dictionary<string, string>
                {
                    { "es", "Cerrar" },
                    { "en", "Close" }
                }
            },
            { "Por favor, espera...", new Dictionary<string, string>
                {
                    { "es", "Por favor, espera..." },
                    { "en", "Please wait..." }
                }
            },
            { "Guardar Ruta", new Dictionary<string, string>
                {
                    { "es", "Guardar Ruta" },
                    { "en", "Save Route" }
                }
            }

            ,
            { "Guardar", new Dictionary<string, string>
                {
                    { "es", "Guardar" },
                    { "en", "Save" }
                }
            },    { "Idioma del sistema", new Dictionary<string, string>
        {
            { "es", "Idioma del sistema" },
            { "en", "System Language" }
        }
    },
    { "Velocidad máxima (m/s)", new Dictionary<string, string>
        {
            { "es", "Velocidad máxima (m/s)" },
            { "en", "Max Speed (m/s)" }
        }
    },
    { "Refresco posición (s)", new Dictionary<string, string>
        {
            { "es", "Refresco posición (s)" },
            { "en", "Position Refresh (s)" }
        }
    },
    { "Elementos para el promedio móvil", new Dictionary<string, string>
        {
            { "es", "Elementos para el promedio móvil" },
            { "en", "Items for Moving Average" }
        }
    },
    { "Filtro de Kalman", new Dictionary<string, string>
        {
            { "es", "Filtro de Kalman" },
            { "en", "Kalman Filter" }
        }
    },
    { "Filtro de Promedio Móvil", new Dictionary<string, string>
        {
            { "es", "Filtro de Promedio Móvil" },
            { "en", "Moving Average Filter" }
        }
    },
    { "Mostrar datos de localización", new Dictionary<string, string>
        {
            { "es", "Mostrar datos de localización" },
            { "en", "Show Localization Data" }
        }
    },
    { "Points:", new Dictionary<string, string>
        {
            { "es", "Puntos:" },
            { "en", "Points:" }
        }
    }
        };


        public static string Translate(string nativePhrase, string currentLanguage)
        {

            if (TranslationDict.ContainsKey(nativePhrase) && TranslationDict[nativePhrase].ContainsKey(currentLanguage))
            {
                return TranslationDict[nativePhrase][currentLanguage];
            }

            return nativePhrase + "...wo Translation";
        }
    }
}
