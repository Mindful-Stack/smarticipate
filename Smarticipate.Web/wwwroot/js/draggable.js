// Makes `panel` draggable by `handle` using pointer events (mouse + touch)
export function makeDraggable(handle, panel) {
    let startX = 0, startY = 0, originLeft = 0, originTop = 0, dragging = false;

    function onDown(e) {
        dragging = true;
        const rect = panel.getBoundingClientRect();
        originLeft = rect.left;
        originTop = rect.top;
        startX = e.clientX;
        startY = e.clientY;
        panel.style.position = 'fixed';
        panel.style.left = originLeft + 'px';
        panel.style.top = originTop + 'px';
        panel.style.right = 'auto';
        panel.style.bottom = 'auto';
        handle.setPointerCapture(e.pointerId);
        e.preventDefault();
    }

    function onMove(e) {
        if (!dragging) return;
        panel.style.left = (originLeft + (e.clientX - startX)) + 'px';
        panel.style.top = (originTop + (e.clientY - startY)) + 'px';
    }

    function onUp(e) {
        dragging = false;
        try {
            handle.releasePointerCapture(e.pointerId);
        } catch {
        }
    }
    
    handle.addEventListener('pointerdown', onDown);
    handle.addEventListener('pointermove', onMove);
    handle.addEventListener('pointerup', onUp);
}