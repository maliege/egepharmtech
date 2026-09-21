// Service worker: siteyi "kurulabilir" yapar ve bağlantı yokken çevrimdışı sayfasını gösterir.
// Blazor Server sunucuyla canlı bağlantı ister; bu yüzden uygulama kabuğu ya da _blazor istekleri
// önbelleğe ALINMAZ. Yalnız gezinme (navigate) istekleri ele alınır: ağ öncelikli, ağ yoksa
// offline.html. Diğer istekler (statik dosyalar, SignalR, CDN) tarayıcıya olduğu gibi bırakılır.
// Her alan adı (ptcalc.tr, ptcalc.net) kendi origin'inde ayrı bir worker çalıştırır.
const CACHE = 'PTCalc-offline-v4';
const OFFLINE_URL = '/offline.html';
const PRECACHE = [OFFLINE_URL, '/img/ptcalc/favicon.svg', '/img/ptcalc/icon-192.png', '/img/ptcalc/icon-512.png'];

self.addEventListener('install', (event) => {
    event.waitUntil(
        caches.open(CACHE)
            .then((cache) => cache.addAll(PRECACHE))
            .then(() => self.skipWaiting())
    );
});

self.addEventListener('activate', (event) => {
    event.waitUntil(
        caches.keys()
            .then((keys) => Promise.all(keys.filter((k) => k !== CACHE).map((k) => caches.delete(k))))
            .then(() => self.clients.claim())
    );
});

self.addEventListener('fetch', (event) => {
    if (event.request.mode !== 'navigate') return;
    event.respondWith(
        fetch(event.request).catch(() =>
            caches.match(OFFLINE_URL).then((r) => r || new Response('Offline', { status: 503, headers: { 'Content-Type': 'text/plain' } }))
        )
    );
});
