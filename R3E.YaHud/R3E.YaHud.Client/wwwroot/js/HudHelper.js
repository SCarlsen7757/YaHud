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

                // Save position as percent (center of widget)
                const leftPx = el.offsetLeft + el.offsetWidth / 2;
                const topPx = el.offsetTop + el.offsetHeight / 2;
                const leftPercent = (leftPx / window.innerWidth) * 100;
                const topPercent = (topPx / window.innerHeight) * 100;

                // Notify C# to update settings
                dotnetHelper.invokeMethodAsync('UpdateWidgetPosition', leftPercent, topPercent);
            }
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
