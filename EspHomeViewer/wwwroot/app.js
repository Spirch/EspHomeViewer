window.downloadFileFromStream = async (fileName, contentStreamReference) => {

    const arrayBuffer = await contentStreamReference.arrayBuffer();
    const blob = new Blob([arrayBuffer]);
    const url = URL.createObjectURL(blob);
    const anchorElement = document.createElement('a');
    anchorElement.href = url;
    anchorElement.download = fileName ?? '';
    anchorElement.click();
    anchorElement.remove();
    URL.revokeObjectURL(url);

}

window.observeAllTables = () => {

    const observedElements = new WeakMap();

    function observeParagraph(p) {
        if (observedElements.has(p)) return;
        observedElements.set(p, true);

        const observer = new MutationObserver(mutations => {
            for (const mutation of mutations) {
                if (mutation.type === "characterData") {
                    const td = p.closest("td");

                    if (td && !observedElements.has(td)) {
                        observedElements.set(td, true);

                        td.classList.add("highlight");
                        setTimeout(() => requestAnimationFrame(() => {
                            td.classList.remove("highlight");
                            observedElements.delete(td); // Clean up
                        }), 1000);
                    }
                }
            }
        });

        observer.observe(p, { subtree: true, characterData: true });
    }

    function observeTable(table) {
        if (observedElements.has(table)) return;
        observedElements.set(table, true);

        table.querySelectorAll("p").forEach(observeParagraph);
    }

    document.querySelectorAll("table").forEach(observeTable);

};