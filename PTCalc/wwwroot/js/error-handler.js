// Global JavaScript error handler for third-party scripts
window.addEventListener('error', function (event) {
    // Google ve diğer harici script hatalarını yakala ve görmezden gel
    const isThirdPartyError = event.filename && (
        event.filename.includes('google.com') ||
        event.filename.includes('gstatic.com') ||
        event.filename.includes('googleapis.com') ||
        event.filename.includes('recaptcha')
    );

    if (isThirdPartyError) {
        console.warn('[Harici Script Hatası Yakalandı]:', event.message);
        event.preventDefault();
        return true;
    }
});

// Unhandled Promise rejection handler
window.addEventListener('unhandledrejection', function (event) {
    const reason = event.reason?.toString() || '';

    // Permissions API veya harici script hatalarını yakala
    if (reason.includes('Permissions') || reason.includes('Illegal invocation')) {
        console.warn('[Harici Script Promise Hatası Yakalandı]:', reason);
        event.preventDefault();
        return true;
    }
});
