// Blazor'dan Bootstrap modal açıp kapatmak için köprü.
//
// ÖNEMLİ: Bu kod eskiden ResetPassword.razor'ın markup'ı içinde bir <script>
// bloğuydu. Blazor, render sırasında DOM'a eklediği <script> etiketlerini
// ÇALIŞTIRMAZ; dolayısıyla window.bootstrapModal hiçbir zaman tanımlanmıyor ve
// her JS interop çağrısı hata veriyordu. Bu yüzden gerçek bir dosyaya taşındı
// ve _Host.cshtml'den yükleniyor. Buraya geri gömmeyin.

window.bootstrapModal = {
    show: function (modalId) {
        const el = document.getElementById(modalId);
        if (!el) throw new Error(`Modal bulunamadı: ${modalId}`);
        if (typeof bootstrap === 'undefined') throw new Error('Bootstrap JS yüklenmemiş.');
        bootstrap.Modal.getOrCreateInstance(el).show();
    },

    hide: function (modalId) {
        const el = document.getElementById(modalId);
        if (!el) return;
        if (typeof bootstrap === 'undefined') return;
        const modal = bootstrap.Modal.getInstance(el);
        if (modal) modal.hide();
    }
};
