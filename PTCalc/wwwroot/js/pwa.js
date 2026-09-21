// Service worker kaydı (kurulabilir uygulama + çevrimdışı sayfası). Kayıt sayfa yüklendikten sonra
// yapılır ki Blazor'un ilk bağlantısıyla yarışmasın. Desteklemeyen tarayıcılarda sessizce geçilir.
(function () {
    if (!('serviceWorker' in navigator)) return;
    window.addEventListener('load', function () {
        navigator.serviceWorker.register('/sw.js', { scope: '/' })
            .catch(function (err) { console.warn('Service worker kaydı başarısız:', err); });
    });
})();
