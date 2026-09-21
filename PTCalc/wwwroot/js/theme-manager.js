// Theme Manager - Koyu/Açık tema yönetimi
window.themeManager = {
    // Tema tercihini localStorage'dan al veya sistem tercihini kullan
    getPreference: function () {
        const stored = localStorage.getItem('theme');
        if (stored) {
            return stored;
        }
        // Sistem tercihini kontrol et
        return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
    },

    // Temayı uygula
    applyTheme: function (theme) {
        document.documentElement.setAttribute('data-theme', theme);
        document.body.setAttribute('data-theme', theme);
        
        if (theme === 'dark') {
            document.documentElement.style.colorScheme = 'dark';
        } else {
            document.documentElement.style.colorScheme = 'light';
        }
        
        localStorage.setItem('theme', theme);
        return theme;
    },

    // Temayı değiştir
    toggleTheme: function () {
        const current = this.getPreference();
        const newTheme = current === 'dark' ? 'light' : 'dark';
        return this.applyTheme(newTheme);
    },

    // Mevcut temayı al
    getCurrentTheme: function () {
        return this.getPreference();
    },

    // Başlangıçta temayı uygula
    initialize: function () {
        const theme = this.getPreference();
        this.applyTheme(theme);
        
        // Sistem tema değişikliğini dinle (sadece localStorage'da tema yoksa)
        window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', (e) => {
            if (!localStorage.getItem('theme')) {
                this.applyTheme(e.matches ? 'dark' : 'light');
            }
        });
        
        return theme;
    }
};

// Sayfa yüklendiğinde temayı uygula (flash önleme)
(function() {
    const theme = localStorage.getItem('theme') || 
        (window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light');
    document.documentElement.setAttribute('data-theme', theme);
    document.documentElement.style.colorScheme = theme;
})();
