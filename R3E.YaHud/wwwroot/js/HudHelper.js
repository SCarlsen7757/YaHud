window.HudHelper = (function () {
    const registry = {}; // elementId -> { el, dotNetRef, isDragging, handlers, targetX, targetY, raf }

    function attachHandlers(entry) {
        if (!entry || entry.handlersAttached) return;
        const el = entry.el;
        const dotnetHelper = entry.dotNetRef;

        function onMouseDown(e) {
            // only left button
            if (e.button !== 0) return;
            entry.isDragging = true;
            entry.startX = e.clientX;
            entry.startY = e.clientY;
            const rect = el.getBoundingClientRect();
            entry.offsetX = e.clientX - rect.left;
            entry.offsetY = e.clientY - rect.top;
            entry.targetX = rect.left;
            entry.targetY = rect.top;

            // capture mousemove on window
            window.addEventListener('mousemove', onMouseMove);
            window.addEventListener('mouseup', onMouseUp);

            // start rAF loop
            if (!entry.raf) entry.raf = requestAnimationFrame(step);
        }

        function onMouseMove(e) {
            if (!entry.isDragging) return;
            // update target positions
            entry.targetX = e.clientX - entry.offsetX;
            entry.targetY = e.clientY - entry.offsetY;
        }

        function onMouseUp(e) {
            if (!entry.isDragging) return;
            entry.isDragging = false;
            window.removeEventListener('mousemove', onMouseMove);
            window.removeEventListener('mouseup', onMouseUp);

            // finalize position based on center
            const rect = el.getBoundingClientRect();
            const leftPx = rect.left + rect.width / 2;
            const topPx = rect.top + rect.height / 2;
            const leftPercent = (leftPx / window.innerWidth) * 100;
            const topPercent = (topPx / window.innerHeight) * 100;

            try {
                dotnetHelper.invokeMethodAsync('UpdateWidgetPosition', leftPercent, topPercent);
            } catch (ex) { 
                console.error('HudHelper: Failed to update widget position', ex); 
            }

        }

        function step() {
            // apply target position
            if (entry.isDragging) {
                // use transform/left/top; keep absolute positioning
                el.style.position = 'absolute';
                const rect = el.getBoundingClientRect();
                el.style.left = Math.max(0, Math.min(window.innerWidth - rect.width, entry.targetX)) + 'px';
                el.style.top = Math.max(0, Math.min(window.innerHeight - rect.height, entry.targetY)) + 'px';
                entry.raf = requestAnimationFrame(step);
            } else {
                if (entry.raf) {
                    cancelAnimationFrame(entry.raf);
                    entry.raf = null;
                }
            }
        }

        function onResize() {
            try {
                dotnetHelper.invokeMethodAsync('OnWindowResize');
            } catch (ex) { 
                console.error('HudHelper: Failed to notify resize', ex); 
            }
        }

        el.addEventListener('mousedown', onMouseDown);
        window.addEventListener('resize', onResize);

        entry.handlers = { onMouseDown, onResize };
        entry.handlersAttached = true;
    }

    function detachHandlers(entry) {
        if (!entry || !entry.handlersAttached) return;
        const el = entry.el;
        const h = entry.handlers;
        try {
            el.removeEventListener('mousedown', h.onMouseDown);
            window.removeEventListener('resize', h.onResize);
        } catch (e) { 
            console.warn('HudHelper: Error detaching handlers', e); 
        }
        entry.handlersAttached = false;
        entry.handlers = null;
    }

    return {
        registerDraggable: function (elementId, dotnetHelper, locked) {
            const el = document.getElementById(elementId);
            if (!el) {
                console.warn('HudHelper.registerDraggable: element not found', elementId);
                return;
            }
            // if already registered, update dotnetRef and locked state
            let entry = registry[elementId];
            if (!entry) {
                entry = { el: el, dotNetRef: dotnetHelper, isDragging: false, handlersAttached: false, raf: null };
                registry[elementId] = entry;
            } else {
                entry.dotNetRef = dotnetHelper;
            }

            if (!locked) attachHandlers(entry);
            else detachHandlers(entry);
        },

        enableDragging: function (elementId) {
            const entry = registry[elementId];
            if (!entry) {
                console.warn('HudHelper.enableDragging: element not registered', elementId);
                return;
            }
            attachHandlers(entry);
        },

        disableDragging: function (elementId) {
            const entry = registry[elementId];
            if (!entry) {
                console.warn('HudHelper.disableDragging: element not registered', elementId);
                return;
            }
            detachHandlers(entry);
        },

        unregisterDraggable: function (elementId) {
            const entry = registry[elementId];
            if (!entry) return;
            detachHandlers(entry);
            try { 
                entry.dotNetRef?.dispose(); 
            } catch (e) { 
                console.warn('HudHelper.unregisterDraggable: Error disposing dotNetRef', e); 
            }
            delete registry[elementId];
        },

        setPosition: function (elementId, xPercent, yPercent) {
            const el = document.getElementById(elementId);
            if (!el) {
                console.warn('HudHelper.setPosition: element not found', elementId);
                return;
            }
            const widgetWidth = el.offsetWidth;
            const widgetHeight = el.offsetHeight;
            el.style.position = "absolute";
            el.style.left = (xPercent / 100 * window.innerWidth) - (widgetWidth / 2) + "px";
            el.style.top = (yPercent / 100 * window.innerHeight) - (widgetHeight / 2) + "px";
        },

        resetPosition: function (elementId, xPercent = 50, yPercent = 50) {
            const el = document.getElementById(elementId);
            if (!el) {
                console.warn('HudHelper.resetPosition: element not found', elementId);
                return;
            }

            // Clear any saved settings
            try { 
                localStorage.removeItem(elementId); 
            } catch (e) { 
                console.warn('HudHelper.resetPosition: localStorage error', e); 
            }

            const widgetWidth = el.offsetWidth;
            const widgetHeight = el.offsetHeight;
            el.style.position = "absolute";
            el.style.left = (xPercent / 100 * window.innerWidth) - (widgetWidth / 2) + "px";
            el.style.top = (yPercent / 100 * window.innerHeight) - (widgetHeight / 2) + "px";
        },

        setWidgetSettings: function (elementId, value) {
            try { 
                localStorage.setItem(elementId, JSON.stringify(value)); 
            } catch (e) { 
                console.error('HudHelper.setWidgetSettings: localStorage error', e); 
            }
        },

        getWidgetSettings: function (elementId) {
            try { 
                const value = localStorage.getItem(elementId); 
                return value ? JSON.parse(value) : null; 
            } catch (e) { 
                console.error('HudHelper.getWidgetSettings: localStorage/JSON error', e); 
                return null; 
            }
        },

        clearWidgetSettings: function (elementId) {
            try { 
                localStorage.removeItem(elementId); 
            } catch (e) { 
                console.warn('HudHelper.clearWidgetSettings: localStorage error', e); 
            }
        }
    };
})();

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
                    } catch (e) { 
                        console.error('colorisHelper.register listener: callback error', e); 
                    }
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
            if (!entry) {
                console.warn('colorisHelper.setColor: picker not found', containerId);
                return;
            }
            try {
                entry.inputEl.value = hex;
                entry.inputEl.style.background = hex;
                // dispatch input event so Coloris and Blazor sync
                const ev = new Event('input', { bubbles: true });
                entry.inputEl.dispatchEvent(ev);
            } catch (e) { 
                console.error('colorisHelper.setColor error', e); 
            }
        },

        unregister: function (containerId) {
            const entry = pickers[containerId];
            if (!entry) return;
            try {
                entry.inputEl.removeEventListener('input', entry.listener);
            } catch (e) { 
                console.warn('colorisHelper.unregister: removeEventListener error', e); 
            }
            delete pickers[containerId];
        },

        // debug helper
        list: function () {
            console.log('coloris pickers:', Object.keys(pickers));
        }
    };
})();
