const highlightClasses = ["border-indigo-400", "bg-indigo-50/40"];

export function bindDropZone(dropZoneElement) {
    const inputElement = dropZoneElement.querySelector('input[type="file"]');
    if (!inputElement) {
        return;
    }

    let dragDepth = 0;

    const setHighlighted = (on) => {
        for (const cls of highlightClasses) {
            dropZoneElement.classList.toggle(cls, on);
        }
    };

    dropZoneElement.addEventListener("dragenter", (e) => {
        e.preventDefault();
        dragDepth++;
        setHighlighted(true);
    });

    dropZoneElement.addEventListener("dragover", (e) => {
        // Required so the browser treats this element as a valid drop target.
        e.preventDefault();
    });

    dropZoneElement.addEventListener("dragleave", (e) => {
        e.preventDefault();
        dragDepth = Math.max(0, dragDepth - 1);
        if (dragDepth === 0) {
            setHighlighted(false);
        }
    });

    dropZoneElement.addEventListener("drop", (e) => {
        e.preventDefault();
        dragDepth = 0;
        setHighlighted(false);

        if (e.dataTransfer?.files?.length) {
            // Blazor's InputFile only reacts to the underlying <input>'s own change event, so move
            // the dropped file onto it and dispatch that event. The input has no "multiple" attribute
            // (single-file upload only), and Blazor throws if more than one file lands on a
            // non-multiple input, so only ever forward the first dropped file via a fresh DataTransfer.
            const dt = new DataTransfer();
            dt.items.add(e.dataTransfer.files[0]);
            inputElement.files = dt.files;
            inputElement.dispatchEvent(new Event("change", { bubbles: true }));
        }
    });
}
