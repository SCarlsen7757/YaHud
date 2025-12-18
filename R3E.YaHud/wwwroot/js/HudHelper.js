window.radarInterop = {
    getOffsetWidth: function (element) {
        if (!element) return 0;   // or null
        return element.offsetWidth;
    }
};



window.HudHelper = (function () {
    const registry = {}; // elementId -> { el, dotNetRef, isDragging, handlers, targetX, targetY, raf, id, collidable }

    // create grid overlay element lazily
    let gridEl = null;
    function ensureGrid() {
        if (gridEl) return;
        gridEl = document.createElement('div');
        gridEl.id = 'hud-grid-overlay';
        gridEl.className = 'hud-grid-overlay';
        gridEl.style.display = 'none';
        gridEl.style.pointerEvents = 'none';
        document.body.appendChild(gridEl);
    }

    function showGrid() {
        ensureGrid();
        gridEl.style.display = 'block';
    }

    function hideGrid() {
        if (!gridEl) return;
        gridEl.style.display = 'none';
    }

    function rectsIntersect(a, b) {
        return !(a.left + a.width <= b.left ||
            b.left + b.width <= a.left ||
            a.top + a.height <= b.top ||
            b.top + b.height <= a.top);
    }

    function getEntryRectAt(entry, left, top) {
        const el = entry.el;
        return { left: left, top: top, width: el.offsetWidth, height: el.offsetHeight };
    }

    function getEntryRect(entry) {
        const r = entry.el.getBoundingClientRect();
        return { left: r.left, top: r.top, width: r.width, height: r.height };
    }

    function wouldCollide(selfEntry, proposedX, proposedY) {
        // if this widget is not collidable, it never collides
        if (!selfEntry.collidable) return false;

        const proposedRect = getEntryRectAt(selfEntry, proposedX, proposedY);

        for (const id in registry) {
            if (!Object.prototype.hasOwnProperty.call(registry, id)) continue;
            if (id === selfEntry.id) continue;
            const other = registry[id];
            if (!other || !other.el) continue;
            // skip non-collidable others
            if (!other.collidable) continue;
            const otherRectDom = getEntryRect(other);
            // skip if not visible or zero-sized
            if (otherRectDom.width === 0 || otherRectDom.height === 0) continue;
            if (rectsIntersect(proposedRect, otherRectDom)) return true;
        }
        return false;
    }

    // Find the maximum distance we can move from (fromX, fromY) towards (toX, toY) without collision
    // Returns the furthest valid position along the movement vector
    function findMaxMovement(entry, fromX, fromY, toX, toY) {
        const dx = toX - fromX;
        const dy = toY - fromY;

        // If no movement, return current position
        if (Math.abs(dx) < 0.1 && Math.abs(dy) < 0.1) {
            return { x: fromX, y: fromY };
        }

        // Binary search for the furthest valid position along the movement vector
        let low = 0.0;
        let high = 1.0;
        let bestT = 0.0;
        const iterations = 10; // More iterations = more precision

        for (let i = 0; i < iterations; i++) {
            const mid = (low + high) / 2;
            const testX = fromX + dx * mid;
            const testY = fromY + dy * mid;

            if (wouldCollide(entry, testX, testY)) {
                high = mid; // Collision detected, search lower half
            } else {
                bestT = mid; // Valid position, try to go further
                low = mid;
            }
        }

        return {
            x: fromX + dx * bestT,
            y: fromY + dy * bestT
        };
    }

    // Try to slide along collision boundary - allows movement perpendicular to blocked axis
    function trySlideMovement(entry, fromX, fromY, toX, toY) {
        const dx = toX - fromX;
        const dy = toY - fromY;

        // Try full movement first - find furthest point along diagonal
        const diagonalResult = findMaxMovement(entry, fromX, fromY, toX, toY);

        // If we reached the target, we're done
        if (Math.abs(diagonalResult.x - toX) < 0.5 && Math.abs(diagonalResult.y - toY) < 0.5) {
            return diagonalResult;
        }

        // We hit something. Try sliding along the axes from the collision point
        const currentX = diagonalResult.x;
        const currentY = diagonalResult.y;

        // Try horizontal slide from collision point
        if (Math.abs(dx) > 0.5) {
            const horizontalResult = findMaxMovement(entry, currentX, currentY, toX, currentY);
            if (Math.abs(horizontalResult.x - currentX) > 0.5) {
                return horizontalResult; // We could slide horizontally
            }
        }

        // Try vertical slide from collision point
        if (Math.abs(dy) > 0.5) {
            const verticalResult = findMaxMovement(entry, currentX, currentY, currentX, toY);
            if (Math.abs(verticalResult.y - currentY) > 0.5) {
                return verticalResult; // We could slide vertically
            }
        }

        // Couldn't slide, return the furthest diagonal position
        return diagonalResult;
    }

    function attachHandlers(entry = {}) {
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
            // store last valid position for sliding
            entry.prevValidX = entry.targetX;
            entry.prevValidY = entry.targetY;

            // show grid overlay while dragging
            try { showGrid(); } catch (ex) { console.warn('HudHelper: showGrid failed', ex); }

            // capture mousemove on window
            window.addEventListener('mousemove', onMouseMove);
            window.addEventListener('mouseup', onMouseUp);

            // start rAF loop
            if (!entry.raf) entry.raf = requestAnimationFrame(step);
        }

        function onMouseMove(e) {
            if (!entry.isDragging) return;

            // compute proposed target positions based on mouse
            const proposedX = e.clientX - entry.offsetX;
            const proposedY = e.clientY - entry.offsetY;

            // clamp to window boundaries
            const maxX = window.innerWidth - el.offsetWidth;
            const maxY = window.innerHeight - el.offsetHeight;
            const clampedX = Math.max(0, Math.min(maxX, proposedX));
            const clampedY = Math.max(0, Math.min(maxY, proposedY));

            // non-collidable widgets can move freely
            if (!entry.collidable) {
                entry.targetX = clampedX;
                entry.targetY = clampedY;
                entry.prevValidX = clampedX;
                entry.prevValidY = clampedY;
                return;
            }

            // For collidable widgets: find the furthest valid position and try to slide along collision boundaries
            const result = trySlideMovement(entry, entry.prevValidX, entry.prevValidY, clampedX, clampedY);

            entry.targetX = result.x;
            entry.targetY = result.y;
            entry.prevValidX = result.x;
            entry.prevValidY = result.y;
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

            // hide grid overlay
            try { hideGrid(); } catch (ex) { console.warn('HudHelper: hideGrid failed', ex); }

            console.log('HudHelper.onMouseUp: calling UpdateWidgetPosition for', entry.id, 'with x=', leftPercent, 'y=', topPercent);
            try {
                dotnetHelper.invokeMethodAsync('UpdateWidgetPosition', leftPercent, topPercent)
                    .catch(err => {
                        console.error('HudHelper: Failed to update widget position (async)', err);
                    });
            } catch (ex) {
                console.error('HudHelper: Failed to update widget position (sync)', ex);
            }
        }

        function step() {
            // apply target position
            if (entry.isDragging) {
                el.style.position = 'absolute';
                el.style.left = Math.max(0, Math.min(window.innerWidth - el.offsetWidth, entry.targetX)) + 'px';
                el.style.top = Math.max(0, Math.min(window.innerHeight - el.offsetHeight, entry.targetY)) + 'px';
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
                dotnetHelper.invokeMethodAsync('OnWindowResize')
                    .catch(err => {
                        console.warn('HudHelper: OnWindowResize failed (widget may be disposed)', err);
                    });
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
        registerDraggable: function (elementId, dotnetHelper, locked, collidable) {
            const el = document.getElementById(elementId);
            if (!el) {
                console.warn('HudHelper.registerDraggable: element not found', elementId);
                return;
            }
            // if already registered, update dotnetRef and locked state
            let entry = registry[elementId];
            if (!entry) {
                entry = { el: el, dotNetRef: dotnetHelper, isDragging: false, handlersAttached: false, raf: null, id: elementId, collidable: !!collidable };
                registry[elementId] = entry;
            } else {
                entry.dotNetRef = dotnetHelper;
                entry.collidable = !!collidable;
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


(function () {
    window.radarInterop = window.radarInterop || {};

    let pendingBatch = null;
    let scheduled = false;

    function applyBatch(batch) {
        if (!Array.isArray(batch)) return;
        for (let i = 0; i < batch.length; i++) {
            const s = batch[i];
            const el = document.getElementById(String(s.id));
            if (!el) continue;

            if (s.display === "none") {
                el.style.display = "none";
                continue;
            } else if (s.display === "") {
                el.style.display = "";
            }

            if (s.transform !== undefined && el.style.transform !== s.transform) el.style.transform = s.transform;
            if (s.opacity !== undefined && el.style.opacity !== s.opacity) el.style.opacity = s.opacity;
            if (s.width !== undefined && el.style.width !== s.width) el.style.width = s.width;
            if (s.height !== undefined && el.style.height !== s.height) el.style.height = s.height;

            if (s.classes !== undefined) {
                el.className = s.classes;
            }
        }
    }

    function scheduleApply(batch) {
        pendingBatch = batch;
        if (scheduled) return;
        scheduled = true;
        window.requestAnimationFrame(() => {
            try {
                applyBatch(pendingBatch);
            } finally {
                pendingBatch = null;
                scheduled = false;
            }
        });
    }

    window.radarInterop.updateDrivers = function (batch) {
        scheduleApply(batch);
    };

})();