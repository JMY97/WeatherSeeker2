// WeatherSeeker console.
//
// Responsibilities are deliberately narrow: read the form, ask our own server
// for data, and paint it. No API key, no third-party host, no innerHTML with
// remote strings -- everything is written with textContent and `use href`
// lookups into the inline sprite, which keeps the page compatible with a strict
// Content-Security-Policy.

(function () {
    "use strict";

    var themeToggle = document.getElementById("theme-toggle");
    var navToggle = document.getElementById("nav-toggle");
    var topbar = document.getElementById("topbar");
    var searchForm = document.getElementById("search-form");
    var cityInput = document.getElementById("city");
    var searchButton = document.getElementById("search-btn");
    var locationButton = document.getElementById("location-btn");
    var notice = document.getElementById("notice");
    var noticeKey = document.getElementById("notice-key");
    var noticeText = document.getElementById("notice-text");

    var currentIcon = document.getElementById("current-icon");
    var stationEl = document.getElementById("current-station");
    var tempEl = document.getElementById("current-temp");
    var condEl = document.getElementById("current-cond");
    var stampEl = document.getElementById("current-stamp");
    var feelsEl = document.getElementById("stat-feels");
    var windEl = document.getElementById("stat-wind");
    var humidityEl = document.getElementById("stat-humidity");
    var pressureEl = document.getElementById("stat-pressure");
    var forecastMeta = document.getElementById("forecast-meta");
    var forecastDays = document.getElementById("forecast-days");
    var meteogram = document.getElementById("meteogram");
    var meteogramSvg = document.getElementById("meteogram-svg");
    var meteogramEmpty = document.getElementById("meteogram-empty");
    var celsiusInput = document.getElementById("celsius");
    var fahrenheitOut = document.getElementById("fahrenheit");

    var SVG_NS = "http://www.w3.org/2000/svg";

    /* ------------------------------------------------------------ theme -- */

    function applyTheme(theme) {
        document.documentElement.setAttribute("data-theme", theme);

        if (themeToggle) {
            var dark = theme === "dark";
            themeToggle.setAttribute("aria-pressed", dark ? "true" : "false");
            themeToggle.setAttribute("aria-label", dark ? "Switch to light theme" : "Switch to dark theme");
        }
    }

    if (themeToggle) {
        applyTheme(document.documentElement.getAttribute("data-theme") || "light");

        themeToggle.addEventListener("click", function () {
            var next = document.documentElement.getAttribute("data-theme") === "dark" ? "light" : "dark";
            applyTheme(next);

            try {
                window.localStorage.setItem("weatherseeker-theme", next);
            } catch (e) {
                /* storage can be blocked; the theme still applies for this view */
            }
        });
    }

    /* -------------------------------------------------------------- nav -- */

    if (navToggle && topbar) {
        navToggle.addEventListener("click", function () {
            var open = topbar.getAttribute("data-nav-open") === "true";
            topbar.setAttribute("data-nav-open", open ? "false" : "true");
            navToggle.setAttribute("aria-expanded", open ? "false" : "true");
            navToggle.setAttribute("aria-label", open ? "Open menu" : "Close menu");
        });
    }

    /* ----------------------------------------------------------- notice -- */

    function showNotice(kind, message) {
        if (!notice || !noticeText || !noticeKey) {
            return;
        }

        noticeKey.textContent = kind;
        noticeText.textContent = message;
        notice.hidden = false;
        notice.classList.toggle("notice--error", kind === "Error");
    }

    function clearNotice() {
        if (notice) {
            notice.hidden = true;
        }
    }

    /* -------------------------------------------------------------- data -- */

    function setBusy(busy) {
        if (searchButton) {
            searchButton.disabled = busy;
            searchButton.textContent = busy ? "Reading…" : "Read conditions";
        }

        if (locationButton) {
            locationButton.disabled = busy;
        }

        document.body.classList.toggle("is-loading", busy);
    }

    function fetchJson(url) {
        return fetch(url, {
            headers: { Accept: "application/json" },
            credentials: "same-origin"
        }).then(function (response) {
            return response.json().catch(function () {
                return {};
            }).then(function (body) {
                if (!response.ok) {
                    var message = body && typeof body.error === "string"
                        ? body.error
                        : "The request failed (" + response.status + ").";
                    throw new Error(message);
                }

                return body;
            });
        });
    }

    /* ------------------------------------------------------------ render -- */

    function iconHref(code) {
        return "#wi-" + (typeof code === "string" && /^[0-9]{2}[dn]$/.test(code) ? code : "03d");
    }

    function setIcon(element, code) {
        if (!element) {
            return;
        }

        var use = element.querySelector("use");
        if (!use) {
            use = document.createElementNS(SVG_NS, "use");
            element.appendChild(use);
        }

        use.setAttribute("href", iconHref(code));
    }

    function formatNumber(value, digits, fallback) {
        if (typeof value !== "number" || !isFinite(value)) {
            return fallback;
        }

        return value.toFixed(digits).replace(/\.0+$/, "");
    }

    function setStat(element, value) {
        if (!element) {
            return;
        }

        var unit = element.querySelector(".stat__u");
        var text = document.createTextNode(value);
        element.textContent = "";
        element.appendChild(text);

        if (unit) {
            element.appendChild(unit);
        }
    }

    function renderCurrent(data) {
        var current = data.current || {};

        if (stationEl) {
            stationEl.textContent = data.station || "Unknown station";
        }

        if (tempEl) {
            tempEl.textContent = formatNumber(current.temperature, 1, "—");
        }

        if (condEl) {
            condEl.textContent = current.condition
                ? current.condition.charAt(0).toUpperCase() + current.condition.slice(1)
                : "Conditions unavailable";
        }

        if (stampEl) {
            stampEl.textContent = current.observedAt
                ? "Observed " + formatTimestamp(current.observedAt)
                : "Observation time unavailable";
        }

        setIcon(currentIcon, current.icon);

        setStat(feelsEl, formatNumber(current.feelsLike, 1, "—") + " ");
        setStat(windEl, formatNumber(current.wind, 1, "—") + " ");
        setStat(humidityEl, formatNumber(current.humidity, 0, "—"));
        setStat(pressureEl, formatNumber(current.pressure, 0, "—"));
    }

    function formatTimestamp(value) {
        var date = new Date(value);

        if (isNaN(date.getTime())) {
            return "just now";
        }

        return date.toLocaleString(undefined, {
            weekday: "short",
            hour: "2-digit",
            minute: "2-digit"
        });
    }

    function renderDays(days) {
        if (!forecastDays) {
            return;
        }

        forecastDays.textContent = "";

        (days || []).forEach(function (day) {
            var li = document.createElement("li");
            li.className = "day";

            var name = document.createElement("span");
            name.className = "day__name";
            name.textContent = day.label || "";

            var date = document.createElement("span");
            date.className = "day__date";
            date.textContent = day.date ? formatDay(day.date) : "";

            var icon = document.createElementNS(SVG_NS, "svg");
            icon.setAttribute("class", "day__icon weather-icon");
            icon.setAttribute("viewBox", "0 0 64 64");
            icon.setAttribute("aria-hidden", "true");

            var use = document.createElementNS(SVG_NS, "use");
            use.setAttribute("href", iconHref(day.icon));
            icon.appendChild(use);

            var temp = document.createElement("span");
            temp.className = "day__temp";
            temp.appendChild(document.createTextNode(formatNumber(day.high, 0, "—")));

            var low = document.createElement("span");
            low.className = "day__low";
            low.textContent = "/ " + formatNumber(day.low, 0, "—");
            temp.appendChild(low);

            var cond = document.createElement("span");
            cond.className = "day__cond";
            cond.textContent = day.condition || "";

            li.appendChild(name);
            li.appendChild(date);
            li.appendChild(icon);
            li.appendChild(temp);
            li.appendChild(cond);
            forecastDays.appendChild(li);
        });
    }

    function formatDay(value) {
        var date = new Date(value + "T00:00:00");

        return isNaN(date.getTime())
            ? value
            : date.toLocaleDateString(undefined, { day: "numeric", month: "short" });
    }

    /* --------------------------------------------------------- meteogram -- */

    function renderMeteogram(days) {
        if (!meteogram || !meteogramSvg || !meteogramEmpty) {
            return;
        }

        var series = (days || []).filter(function (day) {
            return typeof day.high === "number" && typeof day.low === "number";
        });

        if (series.length < 2) {
            meteogram.hidden = true;
            meteogramEmpty.hidden = false;
            return;
        }

        meteogramEmpty.hidden = true;
        meteogram.hidden = false;

        var width = 600;
        var height = 140;
        var padX = 34;
        var padTop = 30;
        var padBottom = 34;

        var highs = series.map(function (d) { return d.high; });
        var lows = series.map(function (d) { return d.low; });
        var max = Math.max.apply(null, highs);
        var min = Math.min.apply(null, lows);
        var span = Math.max(max - min, 1);

        function x(i) {
            return padX + (i * (width - padX * 2)) / (series.length - 1);
        }

        function y(value) {
            return padTop + ((max - value) / span) * (height - padTop - padBottom);
        }

        var highPoints = series.map(function (d, i) { return [x(i), y(d.high)]; });
        var lowPoints = series.map(function (d, i) { return [x(i), y(d.low)]; });

        var highPath = "M" + highPoints.map(function (p) { return p[0].toFixed(1) + " " + p[1].toFixed(1); }).join("L");
        var lowPath = "M" + lowPoints.map(function (p) { return p[0].toFixed(1) + " " + p[1].toFixed(1); }).join("L");

        var areaPath = highPath
            + "L" + lowPoints.slice().reverse().map(function (p) { return p[0].toFixed(1) + " " + p[1].toFixed(1); }).join("L")
            + "Z";

        meteogramSvg.textContent = "";

        var area = document.createElementNS(SVG_NS, "path");
        area.setAttribute("class", "meteogram__fill");
        area.setAttribute("d", areaPath);
        meteogramSvg.appendChild(area);

        var baseline = document.createElementNS(SVG_NS, "line");
        baseline.setAttribute("class", "meteogram__rule");
        baseline.setAttribute("x1", String(padX));
        baseline.setAttribute("x2", String(width - padX));
        baseline.setAttribute("y1", String(y(min)));
        baseline.setAttribute("y2", String(y(min)));
        meteogramSvg.appendChild(baseline);

        [highPath, lowPath].forEach(function (d, index) {
            var path = document.createElementNS(SVG_NS, "path");
            path.setAttribute("class", "meteogram__trace");
            path.setAttribute("d", d);

            if (index === 1) {
                path.setAttribute("stroke-dasharray", "3 5");
                path.setAttribute("opacity", "0.45");
            }

            meteogramSvg.appendChild(path);
        });

        highPoints.forEach(function (point, i) {
            var dot = document.createElementNS(SVG_NS, "circle");
            dot.setAttribute("class", "meteogram__dot");
            dot.setAttribute("cx", point[0].toFixed(1));
            dot.setAttribute("cy", point[1].toFixed(1));
            dot.setAttribute("r", "3");
            meteogramSvg.appendChild(dot);

            var label = document.createElementNS(SVG_NS, "text");
            label.setAttribute("class", "meteogram__label");
            label.setAttribute("x", point[0].toFixed(1));
            label.setAttribute("y", (point[1] - 10).toFixed(1));
            label.setAttribute("text-anchor", "middle");
            label.textContent = formatNumber(series[i].high, 0, "");
            meteogramSvg.appendChild(label);
        });

        if (forecastMeta) {
            forecastMeta.textContent = series[0].label
                ? series[0].label + " — " + series[series.length - 1].label
                : series.length + " days";
        }
    }

    /* ------------------------------------------------------------ search -- */

    function loadCity(city) {
        var query = String(city || "").trim();

        if (!query) {
            showNotice("Error", "Enter a city name first.");
            if (cityInput) {
                cityInput.focus();
            }
            return;
        }

        clearNotice();
        setBusy(true);

        fetchJson("/weather/search?city=" + encodeURIComponent(query))
            .then(function (data) {
                renderCurrent(data);
                renderDays(data.days);
                renderMeteogram(data.days);
                showNotice("Station", "Showing the latest reading for " + (data.station || query) + ".");
            })
            .catch(function (error) {
                showNotice("Error", error.message || "Could not read that station.");
            })
            .then(function () {
                setBusy(false);
            });
    }

    function loadCoords() {
        if (!navigator.geolocation) {
            showNotice("Error", "This browser cannot report a location.");
            return;
        }

        clearNotice();
        setBusy(true);

        navigator.geolocation.getCurrentPosition(
            function (position) {
                var url = "/weather/coords?lat=" + position.coords.latitude.toFixed(4)
                    + "&lon=" + position.coords.longitude.toFixed(4);

                fetchJson(url)
                    .then(function (data) {
                        renderCurrent(data);
                        renderDays(data.days);
                        renderMeteogram(data.days);
                        showNotice("Station", "Showing the latest reading for your location.");
                    })
                    .catch(function (error) {
                        showNotice("Error", error.message || "Could not read your location.");
                    })
                    .then(function () {
                        setBusy(false);
                    });
            },
            function (error) {
                setBusy(false);
                showNotice(
                    "Error",
                    error && error.code === error.PERMISSION_DENIED
                        ? "Location access was denied. Allow it in your browser, or search by city instead."
                        : "Your location could not be determined. Search by city instead.");
            },
            { timeout: 10000, maximumAge: 300000 }
        );
    }

    if (searchForm) {
        searchForm.addEventListener("submit", function (event) {
            event.preventDefault();
            loadCity(cityInput ? cityInput.value : "");
        });
    }

    if (cityInput) {
        cityInput.addEventListener("keydown", function (event) {
            if (event.key === "Enter") {
                event.preventDefault();
                loadCity(cityInput.value);
            }
        });
    }

    if (locationButton) {
        locationButton.addEventListener("click", loadCoords);
    }

    /* --------------------------------------------------------- converter -- */

    function convert() {
        if (!celsiusInput || !fahrenheitOut) {
            return;
        }

        var raw = celsiusInput.value.trim();

        if (raw === "") {
            fahrenheitOut.textContent = "";
            var empty = document.createElement("span");
            empty.textContent = "—";
            fahrenheitOut.appendChild(empty);
            return;
        }

        var celsius = Number(raw);

        if (!isFinite(celsius)) {
            fahrenheitOut.textContent = "";
            var bad = document.createElement("span");
            bad.textContent = "Enter a number";
            fahrenheitOut.appendChild(bad);
            return;
        }

        var fahrenheit = celsius * 1.8 + 32;

        fahrenheitOut.textContent = "";
        fahrenheitOut.appendChild(document.createTextNode(formatNumber(fahrenheit, 1, "—")));

        var unit = document.createElement("span");
        unit.textContent = "\u00B0F";
        fahrenheitOut.appendChild(unit);
    }

    if (celsiusInput) {
        celsiusInput.addEventListener("input", convert);
        convert();
    }
})();
