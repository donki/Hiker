namespace Hiker.Services
{
    public static class Translations
    {
        // Diccionario que contiene las frases nativas y sus traducciones por idioma
        public static Dictionary<string, Dictionary<string, string>> TranslationDict = new Dictionary<string, Dictionary<string, string>>
        {
                        {  "gpxstudio",
    new Dictionary<string, string>
    {
        { "es", "Para crear y editar tus rutas usa <a href=\"https://gpx.studio/\" target=\"_blank\">GPX Studio</a>. Es Gratuito!!! " },
        { "en", "Create and edit your routes with <a href=\"https://gpx.studio/\" target=\"_blank\">GPX Studio</a>. It's Free!!!" }
    } },

            {  "Sabías qué...",
    new Dictionary<string, string>
    {
        { "es", "Sabías qué... " },
        { "en", "You know that..." }
    }
},
            {
    "GPSSignal",
    new Dictionary<string, string>
    {
        { "es", "La cobertura GPS puede ser limitada en zonas montañosas o bosques densos." },
        { "en", "GPS coverage may be limited in mountainous areas or dense forests." }
    }
},
{
    "GPSSignal2",
    new Dictionary<string, string>
    {
        { "es", "En áreas urbanas con edificios altos, la señal GPS puede verse afectada." },
        { "en", "In urban areas with tall buildings, the GPS signal may be affected." }
    }
},
{
    "GPSSignal3",
    new Dictionary<string, string>
    {
        { "es", "Para obtener una mejor precisión, espera unos minutos después de activar el GPS." },
        { "en", "To achieve better accuracy, wait a few minutes after activating GPS." }
    }
},
{
    "Battery",
    new Dictionary<string, string>
    {
        { "es", "Asegúrate de que tu dispositivo tenga suficiente batería antes de iniciar la ruta." },
        { "en", "Ensure your device has enough battery before starting the route." }
    }
},
{
    "GPSSignal4",
    new Dictionary<string, string>
    {
        { "es", "El clima puede afectar la precisión del GPS; tenlo en cuenta." },
        { "en", "Weather can affect GPS accuracy; keep this in mind." }
    }
},
{
    "GPSSignal5",
    new Dictionary<string, string>
    {
        { "es", "Evita confiar únicamente en el GPS; presta atención al entorno." },
        { "en", "Avoid relying solely on GPS; pay attention to your surroundings." }
    }
},
{
    "GPSSignal6",
    new Dictionary<string, string>
    {
        { "es", "En áreas remotas, considera llevar un dispositivo GPS dedicado." },
        { "en", "In remote areas, consider carrying a dedicated GPS device." }
    }
},
{
    "InformToAll",
    new Dictionary<string, string>
    {
        { "es", "Recuerda informar a alguien sobre tu ruta antes de salir." },
        { "en", "Remember to inform someone about your route before leaving." }
    }
},
{
    "GetWater",
    new Dictionary<string, string>
    {
        { "es", "Lleva contigo equipo de emergencia y suficiente agua." },
        { "en", "Carry emergency equipment and enough water with you." }
    }
},
{
    "CheckWeatherNew",
    new Dictionary<string, string>
    {
        { "es", "Verifica las condiciones meteorológicas antes de iniciar tu ruta." },
        { "en", "Check the weather conditions before starting your route." }
    }
},
{
    "SaveNature",
    new Dictionary<string, string>
    {
        { "es", "Sé consciente de tu entorno y respeta la naturaleza." },
        { "en", "Be aware of your surroundings and respect nature." }
    }
},

{
    "Settings",
    new Dictionary<string, string>
    {
        { "es", "Para cambiar la configuración de la aplicación, pulsa el botón <i class='bi bi-gear-fill'></i> en la pantalla de inicio. Desde la pantalla de configuración podrás configurar el idioma." },
        { "en", "To change the application's settings, tap the <i class='bi bi-gear-fill'></i> button on the home screen. From the settings screen, you can configure the language." }
    }
},

            {
    "FollowRoute",
    new Dictionary<string, string>
    {
        { "es", "Para seguir una ruta que ya has cargado, selecciona la opción <i class='bi bi-geo-alt-fill'></i> Seguir Ruta, podrás ver tu ubicación actual en tiempo real con un marcador <i class='bi bi-geo-alt-fill text-success'></i>. Cuando quieras parar vuelve a apretar el botón <i class='bi bi-geo-alt-fill'></i>." },
        { "en", "To follow a route you've already loaded, select the <i class='bi bi-geo-alt-fill'></i> Follow route option; you can see your current location in real-time with a <i class='bi bi-geo-alt-fill text-success'></i> marker. When you want to stop, press the <i class='bi bi-geo-alt-fill'></i> button again." }
    }
},

            {
    "RecordRoute",
    new Dictionary<string, string>
    {
        { "es", "Para grabar una ruta en la pantalla de inicio, selecciona la opción <i class='bi bi-record-fill text-danger'></i> Grabar Ruta. Déja abierta la app mientras recorres la ruta. Al finalizar, selecciona la opción <i class='bi bi-record-fill text-danger'></i> Grabar Ruta para guardar la ruta en tu dispositivo." },
        { "en", "To record a route on the home screen, select the <i class='bi bi-record-fill text-danger'></i> Record Route option. Leave the app open while you traverse the route. At the end, select the <i class='bi bi-record-fill text-danger'></i> Record Route option to save the route to your device." }
    }
},

            {
    "LoadRoute",
    new Dictionary<string, string>
    {
        { "es", "Para cargar una ruta, selecciona la opción <i class='bi bi-cloud-upload-fill'></i> Cargar Ruta. En la siguiente pantalla, selecciona la opción <i class='upload_file'></i> Cargar Ruta. Aparecerá un selector de archivos, selecciona el fichero de ruta que deseas cargar. Finalmente, pulsa el botón <i class='route'></i> para visualizar la ruta en el mapa." },
        { "en", "To load a route, select the <i class='bi bi-cloud-upload-fill'></i> Load Route option. Select the <i class='upload_file'></i> Load Route option. A file selector will appear; select the route file you wish to load. After loading the file, tap the <i class='route'></i> button to display the route on the map." }
    }
},

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
                        { "Accu:", new Dictionary<string, string>
                {
                    { "es", "Prec:" },
                    { "en", "Accu:" }
                }
            },            
            // Frases del componente Acerca de
            { "Acerca de...", new Dictionary<string, string>
                {
                    { "es", "Acerca de..." },
                    { "en", "About..." }
                }
            },
            { "Esta aplicación ha sido desarrollada por Organic Coating para ayudar a los usuarios a rastrear y mapear sus rutas.", new Dictionary<string, string>
                {
                    { "es", "Esta aplicación ha sido desarrollada por Organic Coating para ayudar a los usuarios a rastrear y mapear sus rutas." },
                    { "en", "This application has been developed by Organic Coating to help users track and map their routes." }
                }
},
            {
    "Créditos", new Dictionary<string, string>
                {
                    { "es", "Créditos" },
                    { "en", "Credits" }
                }
            },
            {
    "se utilizan bajo sus términos de licencia. Estos iconos proporcionan un aspecto moderno y elegante a la interfaz de usuario de la aplicación.", new Dictionary<string, string>
                {
                    { "es", "se utilizan bajo sus términos de licencia. Estos iconos proporcionan un aspecto moderno y elegante a la interfaz de usuario de la aplicación." },
                    { "en", "are used under their license terms. These icons provide a modern and stylish look to the app's user interface." }
                }
            },
            {
    "Iconos de", new Dictionary<string, string>
                {
                    { "es", "Iconos de" },
                    { "en", "Icons by" }
                }
            },
            {
    "también se utilizan en esta aplicación, mejorando su usabilidad y diseño visual.", new Dictionary<string, string>
                {
                    { "es", "también se utilizan en esta aplicación, mejorando su usabilidad y diseño visual." },
                    { "en", "are also used in this app, enhancing its usability and visual design." }
                }
            },
            {
    "Esta aplicación utiliza datos de mapas proporcionados por", new Dictionary<string, string>
                {
                    { "es", "Esta aplicación utiliza datos de mapas proporcionados por" },
                    { "en", "This app uses map data provided by" }
                }
            },
            {
    "una fuente libre y abierta de datos de mapas que es mantenida por una comunidad global de colaboradores.", new Dictionary<string, string>
                {
                    { "es", "una fuente libre y abierta de datos de mapas que es mantenida por una comunidad global de colaboradores." },
                    { "en", "a free and open source of map data maintained by a global community of contributors." }
                }
            },
            {
    "La visualización de los mapas y rutas en esta aplicación se realiza mediante", new Dictionary<string, string>
                {
                    { "es", "La visualización de los mapas y rutas en esta aplicación se realiza mediante" },
                    { "en", "The visualization of maps and routes in this app is done using" }
                }
            },
            {
    "una biblioteca JavaScript de código abierto que ofrece funcionalidades de mapas interactivas y altamente personalizables.", new Dictionary<string, string>
                {
                    { "es", "una biblioteca JavaScript de código abierto que ofrece funcionalidades de mapas interactivas y altamente personalizables." },
                    { "en", "an open-source JavaScript library that offers interactive and highly customizable mapping functionalities." }
                }
            },
            {
    "Agradecimientos a Michael Coyle por su librería BlueToque.SharpGPX para la lectura y escritura de archivos GPX.", new Dictionary<string, string>
                {
                    { "es", "Agradecimientos a Michael Coyle por su librería BlueToque.SharpGPX para la lectura y escritura de archivos GPX." },
                    { "en", "Thanks to Michael Coyle for his library BlueToque.SharpGPX for reading and writing GPX files." }
                }
            },
            {
    "Volver a Inicio", new Dictionary<string, string>
                {
                    { "es", "Volver a Inicio" },
                    { "en", "Return to Home" }
                }
            },            {
    "Lat:", new Dictionary<string, string>
                {
                    { "es", "Lat:" },
                    { "en", "Lat:" }
                }
            },
            {
    "Lon:", new Dictionary<string, string>
                {
                    { "es", "Lon:" },
                    { "en", "Lon:" }
                }
            },
            {
    "Cargar", new Dictionary<string, string>
                {
                    { "es", "Cargar" },
                    { "en", "Load" }
                }
            },            {
    "Cargar Ruta", new Dictionary<string, string>
                {
                    { "es", "Cargar Ruta" },
                    { "en", "Load Route" }
                }
            },
            {
    "Seguir Ruta", new Dictionary<string, string>
                {
                    { "es", "Seguir Ruta" },
                    { "en", "Follow Route" }
                }
            },
            {
    "Modo Grabación", new Dictionary<string, string>
                {
                    { "es", "Modo Grabación" },
                    { "en", "Recording Mode" }
                }
            },
            {
    "Configuración", new Dictionary<string, string>
                {
                    { "es", "Configuración" },
                    { "en", "Settings" }
                }
            },
            {
    "Confirmación", new Dictionary<string, string>
                {
                    { "es", "Confirmación" },
                    { "en", "Confirmation" }
                }
            },
            {
    "¿Desea guardar la ruta grabada?", new Dictionary<string, string>
                {
                    { "es", "¿Desea guardar la ruta grabada?" },
                    { "en", "Do you want to save the recorded route?" }
                }
            },
            {
    "Estás aquí...", new Dictionary<string, string>
                {
                    { "es", "Estás aquí..." },
                    { "en", "You are here..." }
                }
            },
            {
    "Grabando...", new Dictionary<string, string>
                {
                    { "es", "Grabando..." },
                    { "en", "Recording..." }
                }
            },
            {
    "Ruta", new Dictionary<string, string>
                {
                    { "es", "Ruta" },
                    { "en", "Route" }
                }
            },
            {
    "Distancia Total", new Dictionary<string, string>
                {
                    { "es", "Distancia Total" },
                    { "en", "Total Distance" }
                }
            },
            {
    "Elevación Mínima", new Dictionary<string, string>
                {
                    { "es", "Elevación Mínima" },
                    { "en", "Minimum Elevation" }
                }
            },
            {
    "Elevación", new Dictionary<string, string>
                {
                    { "es", "Elevación" },
                    { "en", "Maximum" }
                }
            },            {
    "Elevación Máxima", new Dictionary<string, string>
                {
                    { "es", "Elevación Máxima" },
                    { "en", "Maximum Elevation" }
                }
            },
            {
    "Desnivel", new Dictionary<string, string>
                {
                    { "es", "Desnivel" },
                    { "en", "Elevation Gain" }
                }
            },
            {
    "Tiempo Estimado", new Dictionary<string, string>
                {
                    { "es", "Tiempo Estimado" },
                    { "en", "Estimated Time" }
                }
            },
            {
    "Al mapa", new Dictionary<string, string>
                {
                    { "es", "Al mapa" },
                    { "en", "To the Map" }
                }
            },
            {
    "Cerrar", new Dictionary<string, string>
                {
                    { "es", "Cerrar" },
                    { "en", "Close" }
                }
            },
            {
    "Por favor, espera...", new Dictionary<string, string>
                {
                    { "es", "Por favor, espera..." },
                    { "en", "Please wait..." }
                }
            },
            {
    "Guardar Ruta", new Dictionary<string, string>
                {
                    { "es", "Guardar Ruta" },
                    { "en", "Save Route" }
                }
            }

            ,
            {
    "Guardar", new Dictionary<string, string>
                {
                    { "es", "Guardar" },
                    { "en", "Save" }
                }
            },    {
    "Idioma del sistema", new Dictionary<string, string>
        {
            { "es", "Idioma del sistema" },
            { "en", "System Language" }
        }
    },
    {
    "Velocidad máxima (m/s)", new Dictionary<string, string>
        {
            { "es", "Velocidad máxima (m/s)" },
            { "en", "Max Speed (m/s)" }
        }
    },
    {
    "Refresco posición (s)", new Dictionary<string, string>
        {
            { "es", "Refresco posición (s)" },
            { "en", "Position Refresh (s)" }
        }
    },
    {
    "Elementos para el promedio móvil", new Dictionary<string, string>
        {
            { "es", "Elementos para el promedio móvil" },
            { "en", "Items for Moving Average" }
        }
    },
    {
    "Filtro de Kalman", new Dictionary<string, string>
        {
            { "es", "Filtro de Kalman" },
            { "en", "Kalman Filter" }
        }
    },
    {
    "Filtro de Promedio Móvil", new Dictionary<string, string>
        {
            { "es", "Filtro de Promedio Móvil" },
            { "en", "Moving Average Filter" }
        }
    },
    {
    "Mostrar datos de localización", new Dictionary<string, string>
        {
            { "es", "Mostrar datos de localización" },
            { "en", "Show Localization Data" }
        }
    },
    {
    "Points:", new Dictionary<string, string>
        {
            { "es", "Puntos:" },
            { "en", "Points:" }
        }
    },
    {
    "Filtro de precisión", new Dictionary<string, string>
        {
            { "es", "Filtro de precisión" },
            { "en", "Accuracy Filter" }
        }
    },
        {
    "Filtro de velocidad", new Dictionary<string, string>
        {
            { "es", "Filtro de velocidad" },
            { "en", "Speed Filter" }
        }
    },

    {
    "Precisión (m)", new Dictionary<string, string>
        {
            { "es", "Precisión (m)" },
            { "en", "Accuracy (m)" }
        }
},


    {
    "Usar geolocalización nativa MAUI", new Dictionary<string, string>
        {
            { "es", "Usar geolocalización nativa MAUI" },
            { "en", "Use native MAUI localization" }
        }
    },


    {
    "Agradecimientos a Esteban Sequeira por su iconos de banderas", new Dictionary<string, string>
        {
            { "es", "Agradecimientos a Esteban Sequeira por su iconos de banderas" },
            { "en", "Thanks to Esteban Sequeira for their icons flags" }
        }
    },{
                "y de", new Dictionary<string, string>
        {
            { "es", "y de" },
            { "en", "and" }
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
