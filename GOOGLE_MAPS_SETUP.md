# Configuración de Google Maps para Hiker

## ✅ Estado Actual
La aplicación se instaló correctamente en Android y está funcionando. Solo necesita configurar la API key de Google Maps.

## 🗝️ Configurar Google Maps API Key

### 1. Obtener API Key de Google Maps
1. Ve a [Google Cloud Console](https://console.cloud.google.com/)
2. Crea un nuevo proyecto o selecciona uno existente
3. Habilita la API "Maps SDK for Android"
4. Ve a "Credenciales" y crea una nueva API Key
5. Restringe la API Key para Android con el package name: `com.socratic.hiker`

### 2. Configurar la API Key en el proyecto
Edita el archivo `Platforms/Android/AndroidManifest.xml` y reemplaza:
```xml
<meta-data android:name="com.google.android.geo.API_KEY" android:value="AIzaSyDummy_Key_Replace_With_Real_One" />
```

Por:
```xml
<meta-data android:name="com.google.android.geo.API_KEY" android:value="TU_API_KEY_AQUI" />
```

### 3. Recompilar y reinstalar
```bash
dotnet build Hiker.csproj -f net9.0-android
adb install -r "bin\Debug\net9.0-android\com.socratic.hiker-Signed.apk"
```

## 🎯 Funcionalidades que ya funcionan sin API Key
- ✅ Navegación por pestañas (GPS, Rutas, Configuración, Acerca de)
- ✅ Interfaz nativa MAUI
- ✅ Servicios GPS (ubicación, precisión, velocidad)
- ✅ Grabación de rutas
- ✅ Configuración de filtros GPS
- ✅ Gestión de rutas guardadas

## 🗺️ Funcionalidades que requieren API Key
- ❌ Visualización del mapa de Google Maps
- ❌ Mostrar ubicación actual en el mapa
- ❌ Dibujar rutas en el mapa

## 🔧 Alternativa sin API Key
Si no quieres usar Google Maps, puedes:
1. Usar OpenStreetMap con un WebView
2. Mostrar solo coordenadas GPS sin mapa visual
3. Usar mapas offline

La aplicación funciona perfectamente para tracking GPS sin necesidad del mapa visual.