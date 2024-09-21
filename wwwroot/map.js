let map;
let userMarker = null;
let markers = [];
let polyline;

const UsermarkerOptions = {
    title: `Point ${i + 1}`,
    icon: new L.Icon({
        iconUrl: 'images/flecha-abajo.png',
        iconSize: [25, 41], // Tamaño del icono
        iconAnchor: [12, 41], // Ancla del icono (punto de unión con el mapa)
        popupAnchor: [1, -34], // Ancla del popup
    })
};

function initializeMap(message) {
    map = L.map('map').setView([0, 0], 19); // Coordenadas iniciales (0,0) antes de obtener la ubicación real

    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
        maxZoom: 19,
        attribution: '© OpenStreetMap'
    }).addTo(map);



    userMarker = L.marker([0, 0], UsermarkerOptions).addTo(map) // Coordenadas iniciales (0,0)
        .bindPopup(message)
        .openPopup();
    map.zoomControl.remove();
}

function updateMapMarker(message, latitude, longitude, heading = null) {
    if (userMarker) {
        userMarker.setLatLng([latitude, longitude]);
        userMarker.bindPopup(message);
        const currentZoom = map.getZoom();
        map.setView([latitude, longitude], currentZoom); // Mueve el mapa a la posición actual
        if (heading !== null) {
            userMarker.setRotationAngle(heading); // Rotar el marcador según el heading
        }
    }
}

function drawGpxOnMap(gpxContent) {
    if (!map) {
        console.error("Map is not initialized");
        return;
    }

    if (polyline) {
        polyline.remove();
    }

    // Eliminar los marcadores anteriores
    markers.forEach(marker => {
        map.removeLayer(marker);
    });
    markers = []; // Vaciar el array de marcadores

    // Parsear el contenido GPX como XML
    const parser = new DOMParser();
    const gpx = parser.parseFromString(gpxContent, "application/xml");

    const waypoints = gpx.getElementsByTagName("wpt");
    const latLngs = [];

    // Iterar sobre cada punto y agregarlo al mapa
    for (let i = 0; i < waypoints.length; i++) {
        const lat = waypoints[i].getAttribute("lat");
        const lon = waypoints[i].getAttribute("lon");

        const name = waypoints[i].getElementsByTagName("name")[0]?.textContent;

        // Crear un marcador para cada punto
        const markerOptions = {
            title: `Point ${i + 1}`,
            icon: new L.Icon({
                iconUrl: 'images/pin-icon.png',
                iconSize: [25, 41], // Tamaño del icono
                iconAnchor: [12, 41], // Ancla del icono (punto de unión con el mapa)
                popupAnchor: [1, -34], // Ancla del popup
            })
        };

        // Crear y agregar el marcador al mapa
        const marker = L.marker([lat, lon], markerOptions).addTo(map).bindPopup(name);

        // Almacenar el marcador en el array markers
        markers.push(marker);
    }

    // Ajustar la vista del mapa para que todos los puntos sean visibles
    //const bounds = Array.from(waypoints).map(point => [point.getAttribute("lat"), point.getAttribute("lon")]);
    //map.fitBounds(bounds);

    const trackPoints = gpx.getElementsByTagName("trkpt");
    for (let i = 0; i < trackPoints.length; i++) {
        const lat = trackPoints[i].getAttribute("lat");
        const lon = trackPoints[i].getAttribute("lon");
        latLngs.push([lat, lon]);
    }

    if (trackPoints.length > 0) {
        const inilat = trackPoints[0].getAttribute("lat");
        const inilon = trackPoints[0].getAttribute("lon");

        const startMarkerOptions = {
            title: `Inicio`,
            icon: new L.Icon({
                iconUrl: 'images/pin-icon-start.png',
                iconSize: [25, 41],
                iconAnchor: [12, 41],
                popupAnchor: [1, -34],
            })
        };

        const startMarker = L.marker([inilat, inilon], startMarkerOptions).addTo(map);
        markers.push(startMarker);

        const endlat = trackPoints[trackPoints.length - 1].getAttribute("lat");
        const endlon = trackPoints[trackPoints.length - 1].getAttribute("lon");

        const endMarkerOptions = {
            title: `Final`,
            icon: new L.Icon({
                iconUrl: 'images/pin-icon-end.png',
                iconSize: [25, 41],
                iconAnchor: [12, 41],
                popupAnchor: [1, -34],
            })
        };

        const endMarker = L.marker([endlat, endlon], endMarkerOptions).addTo(map);
        markers.push(endMarker);
    }

    if (latLngs.length > 1) {
        polyline = L.polyline(latLngs, {
            color: 'red',  // Cambia el color de la línea si lo deseas
            weight: 3,      // Ancho de la línea
            opacity: 0.7,   // Opacidad de la línea
            smoothFactor: 1 // Factor de suavizado (cuanto más alto, más suave será la línea)
        }).addTo(map);

        // Ajustar la vista del mapa para que todos los waypoints sean visibles
        map.fitBounds(polyline.getBounds());
    }

    calcdistance(gpx);
}

function calcdistance(gpx) {

    const trackPoints = gpx.getElementsByTagName("trkpt");
    let totalDistance = 0;

    for (let i = 1; i < trackPoints.length; i++) {
        const lat1 = parseFloat(trackPoints[i - 1].getAttribute("lat"));
        const lon1 = parseFloat(trackPoints[i - 1].getAttribute("lon"));
        const lat2 = parseFloat(trackPoints[i].getAttribute("lat"));
        const lon2 = parseFloat(trackPoints[i].getAttribute("lon"));

        totalDistance += calculateDistance(lat1, lon1, lat2, lon2);
    }

    // Convertir la distancia total a kilómetros y mostrarla en la página
    const distanceKm = (totalDistance / 1000).toFixed(2);
}

function calculateDistance(lat1, lon1, lat2, lon2) {
    const R = 6371e3; // Radio de la Tierra en metros
    const φ1 = lat1 * Math.PI / 180; // φ, λ en radianes
    const φ2 = lat2 * Math.PI / 180;
    const Δφ = (lat2 - lat1) * Math.PI / 180;
    const Δλ = (lon2 - lon1) * Math.PI / 180;

    const a = Math.sin(Δφ / 2) * Math.sin(Δφ / 2) +
        Math.cos(φ1) * Math.cos(φ2) *
        Math.sin(Δλ / 2) * Math.sin(Δλ / 2);
    const c = 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));

    const d = R * c; // Distancia en metros
    return d;
}

function drawPolyline(recordedLocations) {
    console.log("Recorded Locations received:", recordedLocations); // Depuración para ver los datos

    if (polyline) {
        polyline.remove(); // Eliminar la línea anterior si existe
    }

    if (recordedLocations && recordedLocations.length > 1) {
        const latLngs = [];

        // Usar un bucle for para construir el array de [lat, lon]
        for (let i = 0; i < recordedLocations.length; i++) {
            const loc = recordedLocations[i];
            latLngs.push([loc.latitude, loc.longitude]);
        }

        // Dibujar la polilínea con las coordenadas convertidas
        polyline = L.polyline(latLngs, {
            color: 'red',
            weight: 3,
            opacity: 0.7,
            smoothFactor: 1
        }).addTo(map);
    } else {
        console.error("No valid recordedLocations to draw the polyline");
    }
}


window.triggerFileInputClick = function () {
    document.getElementById('gpxFileInput').click();
};
