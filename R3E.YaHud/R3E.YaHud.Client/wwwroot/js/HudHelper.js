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
                el.style.cursor = 'move';
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
                el.style.cursor = 'default';

                // Save position as percent
                const leftPx = el.offsetLeft + el.offsetWidth / 2;
                const topPx = el.offsetTop + el.offsetHeight / 2;
                const leftPercent = (leftPx / window.innerWidth) * 100;
                const topPercent = (topPx / window.innerHeight) * 100;
                localStorage.setItem(elementId + "_leftPercent", leftPercent);
                localStorage.setItem(elementId + "_topPercent", topPercent);
            }
        });

        function applyPosition(xPercent, yPercent) {
            const widgetWidth = el.offsetWidth;
            const widgetHeight = el.offsetHeight;

            let left = (xPercent / 100 * window.innerWidth) - (widgetWidth / 2);
            let top = (yPercent / 100 * window.innerHeight) - (widgetHeight / 2);

            // bounds check: if widget is fully outside, reset to center
            if (left + widgetWidth < 0 || left > window.innerWidth ||
                top + widgetHeight < 0 || top > window.innerHeight) {
                left = (window.innerWidth / 2) - (widgetWidth / 2);
                top = (window.innerHeight / 2) - (widgetHeight / 2);

                // also reset saved values
                localStorage.removeItem(elementId + "_leftPercent");
                localStorage.removeItem(elementId + "_topPercent");
            }

            el.style.position = "absolute";
            el.style.left = left + "px";
            el.style.top = top + "px";
        }

        // Restore position from localStorage
        const savedX = localStorage.getItem(elementId + "_leftPercent");
        const savedY = localStorage.getItem(elementId + "_topPercent");

        const xPercent = savedX !== null ? parseFloat(savedX) : 50;
        const yPercent = savedY !== null ? parseFloat(savedY) : 50;

        requestAnimationFrame(() => applyPosition(xPercent, yPercent));

        // Also re-check on window resize
        window.addEventListener("resize", () => {
            const savedX = localStorage.getItem(elementId + "_leftPercent") || 50;
            const savedY = localStorage.getItem(elementId + "_topPercent") || 50;
            applyPosition(savedX, savedY);
        });
    },

    setupHotkey: function (hudLockServiceObjRef) {
        document.addEventListener('keydown', function (e) {
            if (e.altKey && e.ctrlKey && e.shiftKey && e.code === 'KeyL') {
                hudLockServiceObjRef.invokeMethodAsync('ToggleLock');
            }
        });
    },

    resetPosition: function (elementId, xPercent = 50, yPercent = 50) {
        const el = document.getElementById(elementId);
        if (!el) return;

        const widgetWidth = el.offsetWidth;
        const widgetHeight = el.offsetHeight;

        el.style.position = "absolute";
        el.style.left = (xPercent / 100 * window.innerWidth) - (widgetWidth / 2) + "px";
        el.style.top = (yPercent / 100 * window.innerHeight) - (widgetHeight / 2) + "px";

        localStorage.removeItem(elementId + "_leftPercent");
        localStorage.removeItem(elementId + "_topPercent");
    }
};
