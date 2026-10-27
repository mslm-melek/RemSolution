// The theme toggle on the sign-in pages. Loaded at the end of the body, once
// the button exists.
(function () {
    var html = document.documentElement;
    var icon = document.getElementById('themeIcon');
    var toggle = document.getElementById('themeToggle');
    if (!icon || !toggle) return;
    // The icon shows what you would GET, like the SPA's toggle.
    function paint() { icon.textContent = html.getAttribute('data-theme') === 'dark' ? 'light_mode' : 'dark_mode'; }
    paint();
    toggle.addEventListener('click', function () {
        var dark = html.getAttribute('data-theme') !== 'dark';
        if (dark) html.setAttribute('data-theme', 'dark'); else html.removeAttribute('data-theme');
        try { localStorage.setItem('remsolution.theme', dark ? 'dark' : 'light'); } catch (e) { }
        paint();
    });
})();
