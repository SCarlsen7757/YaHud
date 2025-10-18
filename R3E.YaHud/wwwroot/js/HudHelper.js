window.HudHelper = {
    makeDraggable: function (elementId, dotnetHelper) {
        const el = document.getElementById(elementId);
        if (!el) return;

        let offsetX, offsetY;
        let isDragging = false;

        el.addEventListener('mousedown', function (e) {
            dotnetHelper.invokeMethodAsync('GetLockState').then(locked => {
                if (locked) return;
                isDragging = true;
                offsetX = e.clientX - el.offsetLeft;
                offsetY = e.clientY - el.offsetTop;
            });
        });

        document.addEventListener('mousemove', function (e) {
            if (isDragging) {
                el.style.left = e.clientX - offsetX + 'px';
                el.style.top = e.clientY - offsetY + 'px';
            }
        });

        document.addEventListener('mouseup', function () {
            if (isDragging) {
                isDragging = false;

                // Save position as percent (center of widget)
                const leftPx = el.offsetLeft + el.offsetWidth / 2;
                const topPx = el.offsetTop + el.offsetHeight / 2;
                const leftPercent = (leftPx / window.innerWidth) * 100;
                const topPercent = (topPx / window.innerHeight) * 100;

                // Notify C# to update settings
                dotnetHelper.invokeMethodAsync('UpdateWidgetPosition', leftPercent, topPercent);
            }
        });

        window.addEventListener('resize', () => {
            // Call C# method to reposition all widgets
            dotnetHelper.invokeMethodAsync('OnWindowResize');
        });
    },

    setPosition: function (elementId, xPercent, yPercent) {
        const el = document.getElementById(elementId);
        if (!el) return;
        const widgetWidth = el.offsetWidth;
        const widgetHeight = el.offsetHeight;
        el.style.position = "absolute";
        el.style.left = (xPercent / 100 * window.innerWidth) - (widgetWidth / 2) + "px";
        el.style.top = (yPercent / 100 * window.innerHeight) - (widgetHeight / 2) + "px";
    },

    resetPosition: function (elementId, xPercent = 50, yPercent = 50) {
        const el = document.getElementById(elementId);
        if (!el) return;

        this.clearWidgetSettings(elementId);

        const widgetWidth = el.offsetWidth;
        const widgetHeight = el.offsetHeight;
        el.style.position = "absolute";
        el.style.left = (xPercent / 100 * window.innerWidth) - (widgetWidth / 2) + "px";
        el.style.top = (yPercent / 100 * window.innerHeight) - (widgetHeight / 2) + "px";
    },

    setWidgetSettings: function (elementId, value) {
        localStorage.setItem(elementId, JSON.stringify(value));
    },

    getWidgetSettings: function (elementId) {
        const value = localStorage.getItem(elementId);
        return value ? JSON.parse(value) : null;
    },

    clearWidgetSettings: function (elementId) {
        localStorage.removeItem(elementId);
    },
};

// Coloris integration helper
window.colorisHelper = (function () {
    const pickers = {}; // containerId -> { el, inputEl, dotNetRef, listener }

    function ensureColorisReady(callback) {
        if (typeof Coloris !== 'undefined') return callback();
        let attempts = 0;
        const maxAttempts = 10;
        const waitMs = 100;
        const tryAgain = function () {
            attempts++;
            if (typeof Coloris !== 'undefined') return callback();
            if (attempts <= maxAttempts) {
                setTimeout(tryAgain, waitMs);
            } else {
                console.warn('colorisHelper: Coloris not available after retries');
            }
        };
        tryAgain();
    }

    return {
        // containerId: logical id used by Blazor
        // inputId: actual input element id in DOM which Coloris will attach to
        // initialColor: hex string
        // dotNetRef: DotNetObjectReference to call back into Blazor
        register: function (containerId, inputId, initialColor, dotNetRef) {
            try {
                const inputEl = document.getElementById(inputId);
                if (!inputEl) {
                    console.warn('colorisHelper.register: input not found', inputId);
                    return;
                }

                // set initial value & background
                inputEl.value = initialColor || '#ffffff';
                inputEl.style.background = initialColor || '#ffffff';
                inputEl.style.color = 'transparent'; // hide text inside swatch

                // input handler to propagate changes to Blazor and update hex readout
                const listener = function (ev) {
                    try {
                        const val = inputEl.value;
                        // update a nearby readonly hex input if exists
                        const hexEl = document.getElementById('hex_' + containerId);
                        if (hexEl) hexEl.value = val;
                        // call back to Blazor
                        if (dotNetRef && typeof dotNetRef.invokeMethodAsync === 'function') {
                            dotNetRef.invokeMethodAsync('NotifyColorChanged', containerId, val);
                        }
                    } catch (e) { console.error(e); }
                };

                inputEl.addEventListener('input', listener, { passive: true });

                pickers[containerId] = { inputEl, dotNetRef, listener };

                // initialize Coloris for this specific input (safe even if Coloris initialized elsewhere)
                ensureColorisReady(function () {
                    try {
                        // attach Coloris to this exact input only
                        Coloris({ el: '#' + inputId });
                    } catch (e) {
                        console.error('colorisHelper: Coloris init failed', e);
                    }
                });
            } catch (e) {
                console.error('colorisHelper.register error', e);
            }
        },

        setColor: function (containerId, hex) {
            const entry = pickers[containerId];
            if (!entry) return;
            try {
                entry.inputEl.value = hex;
                entry.inputEl.style.background = hex;
                // dispatch input event so Coloris and Blazor sync
                const ev = new Event('input', { bubbles: true });
                entry.inputEl.dispatchEvent(ev);
            } catch (e) { console.error(e); }
        },

        unregister: function (containerId) {
            const entry = pickers[containerId];
            if (!entry) return;
            try {
                entry.inputEl.removeEventListener('input', entry.listener);
            } catch (e) { /* ignore */ }
            delete pickers[containerId];
        },

        // debug helper
        list: function () {
            console.log('coloris pickers:', Object.keys(pickers));
        }
    };
})();
