// Loaded synchronously in the head of the sign-in pages, so it runs before the
// first paint and a dark-mode user never sees a white flash. Same storage key
// and default as the SPA (shared/theme.ts). A file rather than an inline
// script because the Content-Security-Policy allows scripts from 'self' only.
(function () {
    var choice = null;
    try { choice = localStorage.getItem('remsolution.theme'); } catch (e) { }
    var dark = choice === 'dark' ||
        ((!choice || choice === 'system') && window.matchMedia && matchMedia('(prefers-color-scheme: dark)').matches);
    if (dark) document.documentElement.setAttribute('data-theme', 'dark');
})();
