/* Makes `panel` draggable by `handle`. Keeps it above the app's own UI, on-screen,
and remembers where it was left. Pure DOM, it stays inside the page (cannot float over other application windows).*/

const BASE_Z = 10000;
let zCounter = BASE_Z;
const EDGE = 24;

export function makeDraggable(handle, panel, storageKey) {
    let startX = 0, startY = 0, originLeft = 0, originTop = 0, dragging = false;
    
    function bringToFront(){
        panel.style.zIndex = (++zCounter).toString();
    }

    // Switch from the CSS top/right anchor to explicit fixed left/top.
    function pin() {
        const rect = panel.getBoundingClientRect();
        panel.style.position = 'fixed';
        panel.style.left = rect.left + 'px';
        panel.style.top = rect.top + 'px';
        panel.style.right = 'auto';
        panel.style.bottom = 'auto';
    }

    function clampToViewport() {
        const rect = panel.getBoundingClientRect();
        let left = parseFloat(panel.style.left);
        let top = parseFloat(panel.style.top);
        if (Number.isNaN(left)) left = rect.left;
        if (Number.isNaN(top)) top = rect.top;

        const minLeft = EDGE - rect.width;          // mostly off the left, edge still grabbable
        const maxLeft = window.innerWidth - EDGE;
        const minTop = 0;                            // the handle never leaves the top edge
        const maxTop = window.innerHeight - EDGE;

        panel.style.left = Math.min(Math.max(left, minLeft), maxLeft) + 'px';
        panel.style.top = Math.min(Math.max(top, minTop), maxTop) + 'px';
    }

    function save() {
        if (!storageKey) return;
        try {
            localStorage.setItem(storageKey, JSON.stringify({ left: panel.style.left, top: panel.style.top }));
        } catch { }
    }

    function restore() {
        if (!storageKey) return;
        try {
            const raw = localStorage.getItem(storageKey);
            if (!raw) return;
            const { left, top } = JSON.parse(raw);
            if (!left || !top) return;
            pin();
            panel.style.left = left;
            panel.style.top = top;
            clampToViewport();
        } catch { }
    }

    function onDown(e) {
        // Let clicks on interactive controls in the handle (e.g. the reset button) through.
        if (e.target.closest('button, a, input, textarea, select')) return;
        dragging = true;
        pin();
        const rect = panel.getBoundingClientRect();
        originLeft = rect.left;
        originTop = rect.top;
        startX = e.clientX;
        startY = e.clientY;
        handle.setPointerCapture(e.pointerId);
        e.preventDefault();
    }

    function onMove(e) {
        if (!dragging) return;
        panel.style.left = (originLeft + (e.clientX - startX)) + 'px';
        panel.style.top = (originTop + (e.clientY - startY)) + 'px';
        clampToViewport();
    }

    function onUp(e) {
        if (!dragging) return;
        dragging = false;
        try { handle.releasePointerCapture(e.pointerId); } catch { }
        clampToViewport();
        save();
    }

    // Re-clamp whenever the viewport changes
    function onResize() { clampToViewport(); }

    panel.addEventListener('pointerdown', bringToFront);
    handle.addEventListener('pointerdown', onDown);
    handle.addEventListener('pointermove', onMove);
    handle.addEventListener('pointerup', onUp);
    window.addEventListener('resize', onResize);

    bringToFront(); 
    restore();

    // Returned to Blazor so DraggablePanel can detach the window listener on dispose
    return {
        dispose() {
            panel.removeEventListener('pointerdown', bringToFront);
            handle.removeEventListener('pointerdown', onDown);
            handle.removeEventListener('pointermove', onMove);
            handle.removeEventListener('pointerup', onUp);
            window.removeEventListener('resize', onResize);
        }
    };
}