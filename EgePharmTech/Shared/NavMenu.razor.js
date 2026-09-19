// Scroll efektini başlat
window.initializeScrollEffect = () => {
    window.addEventListener('scroll', function () {
        const navbar = document.querySelector('.navbar');
        const defaultButtons = document.querySelector('.default-buttons');
        const scrolledButtons = document.querySelector('.scrolled-buttons');

        if (window.scrollY > 50) {
            navbar?.classList.add('navbar-scrolled');
            defaultButtons?.classList.add('d-none');
            scrolledButtons?.classList.remove('d-none');
        } else {
            navbar?.classList.remove('navbar-scrolled');
            defaultButtons?.classList.remove('d-none');
            scrolledButtons?.classList.add('d-none');
        }
    });

    const menuItems = document.querySelectorAll('.navbar-nav .nav-link, .default-buttons a, .scrolled-buttons a');
    menuItems.forEach(item => {
        item.addEventListener('click', (event) => {
            const navbarCollapse = document.querySelector('.navbar-collapse');
            const isDropdownToggle = item.classList.contains('dropdown-toggle');

            if (!isDropdownToggle && navbarCollapse?.classList.contains('show')) {
                navbarCollapse.classList.remove('show');
            }
        });
    });

    const dropdownLinks = document.querySelectorAll('.dropdown-menu a');
    dropdownLinks.forEach(link => {
        link.addEventListener('click', () => {
            const navbarCollapse = document.querySelector('.navbar-collapse');
            if (navbarCollapse?.classList.contains('show')) {
                navbarCollapse.classList.remove('show');
            }
        });
    });
};