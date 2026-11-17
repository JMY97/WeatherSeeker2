// Applies the stored or system theme before first paint so the page never
// flashes the wrong colour scheme. Kept in its own file so the layout can load
// it in <head> without an inline script, which the CSP forbids.
(function () {
    var root = document.documentElement;
    var stored = null;

    try {
        stored = window.localStorage.getItem("weatherseeker-theme");
    } catch (e) {
        stored = null;
    }

    var prefersDark = window.matchMedia("(prefers-color-scheme: dark)").matches;
    var theme = stored === "dark" || stored === "light" ? stored : (prefersDark ? "dark" : "light");

    root.setAttribute("data-theme", theme);
})();
