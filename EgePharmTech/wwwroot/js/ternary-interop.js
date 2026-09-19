// Üçgen faz diyagramı sayfası: tarayıcıda saklama ve dosya indirme yardımcıları.
// Blazor Server'da dosya indirmek için JS gerekir (sunucu tarafında Blob yok);
// localStorage da yalnız tarayıcıda var.
window.ternaryStore = {
    save: function (key, json) {
        try { localStorage.setItem(key, json); return true; } catch (e) { return false; }
    },
    load: function (key) {
        try { return localStorage.getItem(key); } catch (e) { return null; }
    },
    remove: function (key) {
        try { localStorage.removeItem(key); } catch (e) { /* yoksay */ }
    },

    downloadText: function (filename, text, mime) {
        const blob = new Blob([text], { type: mime || 'text/plain;charset=utf-8' });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = filename;
        document.body.appendChild(a);
        a.click();
        a.remove();
        setTimeout(function () { URL.revokeObjectURL(url); }, 1000);
    },

    // Ekrandaki SVG'yi tek başına açılabilir bir dosyaya çevirir. Sayfadaki SVG tema
    // renklerini CSS değişkenlerinden (class ile) alıyor; dosyada CSS olmayacağı için
    // hesaplanmış renkler öznitelik olarak gömülür, zemin için de bir dikdörtgen eklenir.
    exportSvgString: function (elementId) {
        const svg = document.getElementById(elementId);
        if (!svg) return null;

        const clone = svg.cloneNode(true);
        const src = svg.querySelectorAll('*');
        const dst = clone.querySelectorAll('*');
        for (let i = 0; i < src.length; i++) {
            if (src[i].classList && src[i].classList.length > 0) {
                const cs = getComputedStyle(src[i]);
                if (cs.fill && cs.fill !== 'none') dst[i].setAttribute('fill', cs.fill);
                if (cs.stroke && cs.stroke !== 'none') dst[i].setAttribute('stroke', cs.stroke);
                dst[i].removeAttribute('class');
            }
        }

        const vb = svg.viewBox && svg.viewBox.baseVal;
        const w = vb && vb.width ? vb.width : svg.clientWidth;
        const h = vb && vb.height ? vb.height : svg.clientHeight;
        clone.setAttribute('xmlns', 'http://www.w3.org/2000/svg');
        clone.setAttribute('width', String(w));
        clone.setAttribute('height', String(h));
        clone.removeAttribute('class');
        clone.removeAttribute('style');
        clone.removeAttribute('id');

        const bg = getComputedStyle(svg).backgroundColor;
        if (bg && bg !== 'rgba(0, 0, 0, 0)' && bg !== 'transparent') {
            const rect = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
            rect.setAttribute('x', '0'); rect.setAttribute('y', '0');
            rect.setAttribute('width', String(w)); rect.setAttribute('height', String(h));
            rect.setAttribute('fill', bg);
            clone.insertBefore(rect, clone.firstChild);
        }

        // Blazor'un olay dinleyici izleri dosyada anlamsız
        clone.querySelectorAll('[_bl_]').forEach(function (el) { el.removeAttribute('_bl_'); });
        Array.prototype.forEach.call(clone.querySelectorAll('*'), function (el) {
            Array.prototype.slice.call(el.attributes).forEach(function (a) {
                if (a.name.indexOf('_bl_') === 0 || a.name.indexOf('blazor:') === 0) el.removeAttribute(a.name);
            });
        });

        return '<?xml version="1.0" encoding="UTF-8"?>\n' + new XMLSerializer().serializeToString(clone);
    },

    downloadSvg: function (elementId, filename) {
        const xml = window.ternaryStore.exportSvgString(elementId);
        if (!xml) return false;
        window.ternaryStore.downloadText(filename, xml, 'image/svg+xml;charset=utf-8');
        return true;
    }
};
