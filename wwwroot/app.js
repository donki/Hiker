let wakeLock = null;

async function requestWakeLock() {
    try {
        wakeLock = await navigator.wakeLock.request('screen');
        console.log('Wake Lock is active.');
    } catch (err) {
        console.error(`${err.name}, ${err.message}`);
    }
}

function releaseWakeLock() {
    if (wakeLock !== null) {
        wakeLock.release()
            .then(() => {
                wakeLock = null;
                console.log('Wake Lock is released.');
            });
    }
}

function initializeAd() {
    // Este ejemplo muestra un contenedor simple de anuncio
    /*const adContainer = document.getElementById('ad-container');

    adContainer.style.position = 'fixed';
    adContainer.style.bottom = '0';
    adContainer.style.width = '100%';
    adContainer.style.zIndex = '1000';
    adContainer.style.backgroundColor = 'white';

    // Inserta el contenedor en el body
    document.body.appendChild(adContainer);

    // Crea un script de AdSense
    var adScript = document.createElement('script');
    adScript.async = true;
    adScript.src = "https://pagead2.googlesyndication.com/pagead/js/adsbygoogle.js";
    adContainer.appendChild(adScript);

    // Crear un elemento de anuncio
    var ad = document.createElement('ins');
    ad.className = 'adsbygoogle';
    ad.style.display = 'block';
    ad.style.textAlign = 'center';
    ad.setAttribute('data-ad-client', 'ca-app-pub-9988501757623315~2511187344'); // Reemplaza con tu ID de cliente
    //ad.setAttribute('data-ad-slot', 'Mapa'); // Reemplaza con tu Ad Unit ID
    ad.setAttribute('data-ad-format', 'auto');
    adContainer.appendChild(ad);

    // Cargar el anuncio
    (adsbygoogle = window.adsbygoogle || []).push({});*/
}