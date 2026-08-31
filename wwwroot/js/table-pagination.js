// Client-side pagination for data tables: once a table's tbody has more than
// PAGE_SIZE rows, only PAGE_SIZE show at a time and a Prev/Next control is
// appended below it - so a long table never expands the page instead of
// scrolling internally. Tables opt out with data-no-paginate.
//
// Paginates only whatever a page's own search/status/role filter currently
// leaves visible (see isFilteredOut below) - a filtered table's own script
// must call TFPagination.init(table) right after it updates row visibility,
// so the page count, Prev/Next state, and the nav itself (hidden entirely
// once the filtered set is small enough to fit on one page) all match the
// current filter instead of the full unfiltered row count.
window.TFPagination = (function () {
    const PAGE_SIZE = 6;

    // Several index tables keep a permanent "No X found" sentinel row in the
    // tbody (id="emptyRow", or id="noYearMatch" on the two PerformanceRecords
    // pages) that a search/filter script shows or hides on its own - it's
    // still there, just display:none, even while real rows are showing.
    // Pagination must never count it as a data row or touch its display:
    // doing so could flip it back to visible on page 1 (or steal a row slot
    // from real data) even though the table isn't actually empty.
    const SENTINEL_ROW_IDS = new Set(["emptyRow", "noYearMatch"]);

    // Marks a row pagination itself hid for being off the current page - as
    // opposed to a page's own filter hiding it. Hidden purely through this
    // class's !important CSS rule (site.css), never through style.display,
    // so pagination's own hiding never shares state with (and can never be
    // clobbered by, or clobber) a page's filter, which hides rows via plain
    // style.display or Bootstrap's .d-none - see isFilteredOut below.
    const PAGE_HIDDEN_CLASS = "tf-page-hidden";

    // A row a page's own search/status/role filter has ruled out - either by
    // setting style.display = "none" directly (most filterTable() functions)
    // or by toggling Bootstrap's .d-none class (the PerformanceRecords period/
    // year filters). This never looks at PAGE_HIDDEN_CLASS, so it reflects
    // only the page's filter, regardless of pagination's own state; a page's
    // filter script is expected to call TFPagination.init(table) again right
    // after it finishes updating which rows are hidden, so this rebuild sees
    // the current filtered set.
    function isFilteredOut(row) {
        return row.style.display === "none" || row.classList.contains("d-none");
    }

    function build(table) {
        if (!table || !table.tBodies || !table.tBodies[0]) return;
        const tbody = table.tBodies[0];

        if (table._tfPaginationNav) {
            table._tfPaginationNav.remove();
            table._tfPaginationNav = null;
        }

        // Drop the previous build's off-page marks. Since pagination hides
        // rows only via this class (never style.display or d-none), this
        // can't undo or interact with whatever the page's filter has set.
        Array.from(tbody.rows).forEach(r => r.classList.remove(PAGE_HIDDEN_CLASS));

        const rows = Array.from(tbody.rows).filter(r => !SENTINEL_ROW_IDS.has(r.id) && !isFilteredOut(r));
        if (rows.length <= PAGE_SIZE) {
            return;
        }

        const pageCount = Math.ceil(rows.length / PAGE_SIZE);
        let page = 1;

        const nav = document.createElement("div");
        nav.className = "tf-pagination";

        const prevBtn = document.createElement("button");
        prevBtn.type = "button";
        prevBtn.className = "tf-pagination-btn";
        prevBtn.innerHTML = '<i class="bi bi-chevron-left"></i>';

        const info = document.createElement("span");
        info.className = "tf-pagination-info";

        const nextBtn = document.createElement("button");
        nextBtn.type = "button";
        nextBtn.className = "tf-pagination-btn";
        nextBtn.innerHTML = '<i class="bi bi-chevron-right"></i>';

        function render() {
            const start = (page - 1) * PAGE_SIZE;
            rows.forEach((r, i) => {
                const onPage = i >= start && i < start + PAGE_SIZE;
                r.classList.toggle(PAGE_HIDDEN_CLASS, !onPage);
            });
            info.textContent = "Page " + page + " of " + pageCount;
            prevBtn.disabled = page === 1;
            nextBtn.disabled = page === pageCount;
        }

        prevBtn.addEventListener("click", function () {
            if (page > 1) { page--; render(); }
        });
        nextBtn.addEventListener("click", function () {
            if (page < pageCount) { page++; render(); }
        });

        nav.appendChild(prevBtn);
        nav.appendChild(info);
        nav.appendChild(nextBtn);

        // Anchor after the whole card the table sits in (.wlm-card / .vm-card),
        // not just its .table-responsive scroll wrapper - otherwise the nav
        // lands inside the same bordered box as the table and reads as part
        // of it instead of a control below it.
        const anchor = table.closest(".wlm-card, .vm-card") || table.closest(".table-responsive") || table;
        anchor.insertAdjacentElement("afterend", nav);
        table._tfPaginationNav = nav;

        render();
    }

    function initAll(root) {
        (root || document).querySelectorAll("table.table:not([data-no-paginate])").forEach(build);
    }

    document.addEventListener("DOMContentLoaded", function () {
        initAll(document);
    });

    return { init: build, initAll: initAll };
})();
