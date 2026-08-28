// Shared half-star rating widget for Performance Evaluation.
//
// Markup for one star (built by buildStars below):
//   <span class="star">
//     <span class="star-bg"><i class="bi bi-star-fill"></i></span>
//     <span class="star-fg" style="width:0"><i class="bi bi-star-fill"></i></span>
//   </span>
// The grey background star is always fully drawn; the blue foreground star is
// clipped to 0%, 50% or 100% width. That's what produces a real half star.
//
// Interactive use: a container with [data-star-input] and data-target pointing
// at a hidden input holding the value. Clicking the left half of a star selects
// x.5, the right half selects x.0. Clicking the current value again clears it.
// The hidden input fires a 'change' event so score previews can recalculate.
//
// Read-only use: window.StarRating.render(value) returns star markup, used to
// fill in view modals that are built in JavaScript.
(function () {
    const MAX_STARS = 4;
    const STEP = 0.5;

    function clampValue(value) {
        const n = parseFloat(value);
        if (isNaN(n)) return 0;
        return Math.min(Math.max(n, 0), MAX_STARS);
    }

    // Snapped to the nearest half star - the value we actually store.
    function snap(value) {
        return Math.round(clampValue(value) / STEP) * STEP;
    }

    // How much of star `index` (1-based) should be blue, as a percentage.
    function fillPercent(value, index) {
        const filled = clampValue(value) - (index - 1);
        if (filled >= 1) return 100;
        if (filled <= 0) return 0;
        return filled * 100;
    }

    function starMarkup(value) {
        let html = '';
        for (let i = 1; i <= MAX_STARS; i++) {
            html += '<span class="star" data-index="' + i + '">' +
                '<span class="star-bg"><i class="bi bi-star-fill"></i></span>' +
                '<span class="star-fg" style="width:' + fillPercent(value, i) + '%"><i class="bi bi-star-fill"></i></span>' +
                '</span>';
        }
        return html;
    }

    function paint(container, value) {
        container.querySelectorAll('.star').forEach(star => {
            const index = parseInt(star.dataset.index, 10);
            const fg = star.querySelector('.star-fg');
            if (fg) fg.style.width = fillPercent(value, index) + '%';
        });

        const scope = container.closest('td') || container.parentElement;
        const label = scope ? scope.querySelector('.star-value-label') : null;
        if (label) {
            const weight = parseFloat(label.dataset.weight || '0');
            const points = weight ? (weight * value / MAX_STARS) : 0;
            label.textContent = value === 0
                ? 'Not rated'
                : value + ' / ' + MAX_STARS + ' stars' +
                (weight ? ' \u2022 ' + points.toFixed(2) + ' / ' + weight + ' pts' : '');
        }
    }

    // The value the pointer is currently over: left half of a star gives x.5,
    // right half gives x.0.
    function valueAtPointer(container, event) {
        const star = event.target.closest('.star');
        if (!star || !container.contains(star)) return null;

        const index = parseInt(star.dataset.index, 10);
        const rect = star.getBoundingClientRect();
        const isLeftHalf = (event.clientX - rect.left) < rect.width / 2;
        return isLeftHalf ? index - STEP : index;
    }

    function wire(container) {
        if (container.dataset.starWired === 'true') return;
        container.dataset.starWired = 'true';

        const target = document.getElementById(container.dataset.target);
        if (!target) return;

        // Build the stars if the server didn't already render them.
        if (!container.querySelector('.star')) container.innerHTML = starMarkup(0);

        const committed = () => snap(target.value || '0');
        paint(container, committed());

        const interactive = () => !container.classList.contains('readonly') && !target.disabled;

        container.addEventListener('mousemove', function (e) {
            if (!interactive()) return;
            const preview = valueAtPointer(container, e);
            if (preview !== null) paint(container, preview);
        });

        container.addEventListener('mouseleave', function () {
            paint(container, committed());
        });

        container.addEventListener('click', function (e) {
            if (!interactive()) return;

            const picked = valueAtPointer(container, e);
            if (picked === null) return;

            // Clicking the current rating again clears it, so a mis-click is
            // easy to undo without a separate control.
            const next = (picked === committed()) ? 0 : picked;

            target.value = next;
            paint(container, next);
            target.dispatchEvent(new Event('change', { bubbles: true }));
        });

        // Keyboard support: arrows nudge by half a star.
        container.setAttribute('tabindex', '0');
        container.setAttribute('role', 'slider');
        container.addEventListener('keydown', function (e) {
            if (!interactive()) return;
            let next = committed();

            if (e.key === 'ArrowRight' || e.key === 'ArrowUp') next = Math.min(next + STEP, MAX_STARS);
            else if (e.key === 'ArrowLeft' || e.key === 'ArrowDown') next = Math.max(next - STEP, 0);
            else if (e.key === 'Home') next = 0;
            else if (e.key === 'End') next = MAX_STARS;
            else return;

            e.preventDefault();
            target.value = next;
            paint(container, next);
            target.dispatchEvent(new Event('change', { bubbles: true }));
        });
    }

    function initAll(root) {
        (root || document).querySelectorAll('[data-star-input]').forEach(wire);
    }

    // Read-only star markup for a 0-4 value, including partial fills.
    function render(value, extraClass) {
        const cls = (extraClass === undefined) ? 'sm' : extraClass;
        return '<span class="star-rating readonly ' + cls + '">' + starMarkup(clampValue(value)) + '</span>';
    }

    window.StarRating = {
        initAll: initAll,
        render: render,
        paint: paint,
        snap: snap,
        MAX_STARS: MAX_STARS,
        STEP: STEP
    };

    document.addEventListener('DOMContentLoaded', function () { initAll(document); });
})();
