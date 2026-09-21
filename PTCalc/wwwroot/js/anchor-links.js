// Sayfa içi çapa bağlantıları (#bolum) Blazor Server'da iki nedenle çalışmaz:
//  1. <base href="/"> yüzünden href="#x" ana sayfaya (/#x) çözülür,
//  2. Blazor tıklamayı yakalayıp yönlendirme yapar, kaydırma yapmaz.
// Bu betik aynı sayfaya işaret eden hash bağlantılarını capture aşamasında
// (Blazor'ın document dinleyicisinden önce) yakalar, hedefe kaydırır ve
// adres çubuğunu günceller. Sayfa ilk açılışta hash ile geldiyse (derin bağlantı),
// Blazor içerik ürettikten sonra hedefe kaydırır. CSS'teki scroll-margin-top
// sabit başlık payını verir.
(function () {
    "use strict";

    function scrollToHash(hash, smooth) {
        if (!hash || hash.length < 2) return false;
        var id;
        try { id = decodeURIComponent(hash.slice(1)); } catch (e) { id = hash.slice(1); }
        var el = document.getElementById(id);
        if (!el) return false;
        el.scrollIntoView({ behavior: smooth ? "smooth" : "auto", block: "start" });
        return true;
    }

    document.addEventListener("click", function (e) {
        if (e.defaultPrevented || e.button !== 0 || e.metaKey || e.ctrlKey || e.shiftKey || e.altKey) return;
        var a = e.target && e.target.closest ? e.target.closest("a[href]") : null;
        if (!a || a.target && a.target !== "_self") return;
        var raw = a.getAttribute("href") || "";
        var url;
        try { url = new URL(raw, raw.charAt(0) === "#" ? location.href : document.baseURI); } catch (err) { return; }
        if (!url.hash || url.origin !== location.origin || url.pathname !== location.pathname) return;
        if (!document.getElementById(decodeURIComponent(url.hash.slice(1)))) return;
        e.preventDefault();
        e.stopImmediatePropagation();
        scrollToHash(url.hash, true);
        if (location.hash !== url.hash) history.pushState(null, "", url.pathname + url.search + url.hash);
    }, true);

    window.addEventListener("hashchange", function () { scrollToHash(location.hash, true); });

    // Derin bağlantı: Blazor devresi kurulup içerik gelince hedef görünür olur.
    if (location.hash) {
        var tries = 0;
        var timer = setInterval(function () {
            if (scrollToHash(location.hash, false) || ++tries > 40) clearInterval(timer);
        }, 150);
    }
})();
