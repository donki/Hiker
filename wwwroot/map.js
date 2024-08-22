let map;
let userMarker;

function initializeMap() {
    map = L.map('map').setView([0, 0], 13); // Coordenadas iniciales (0,0) antes de obtener la ubicación real

    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
        maxZoom: 19,
        attribution: '© OpenStreetMap'
    }).addTo(map);

    userMarker = L.marker([0, 0]).addTo(map) // Coordenadas iniciales (0,0)
        .bindPopup('Aquí estás')
        .openPopup();
}

function updateMapMarker(latitude, longitude) {
    if (userMarker) {
        userMarker.setLatLng([latitude, longitude]);
        map.setView([latitude, longitude], 13); // Mueve el mapa a la posición actual
    }
}
