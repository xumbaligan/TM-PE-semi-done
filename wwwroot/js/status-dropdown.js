// Wires a Bootstrap dropdown built from the .status-dropdown-btn/.status-dropdown-menu
// markup so it behaves like the native <select> it replaces: a hidden input carries
// the value that actually posts with the form, the button shows the picked item's
// label, and the picked item gets .active - mirroring a <select>'s value/change
// event while still allowing the custom bordered/dark-purple styling a native
// dropdown's option list can't fully take on.
function wireStatusDropdown(btnId, hiddenInput, options) {
    options = options || {};
    const button = document.getElementById(btnId);
    const menu = document.querySelector('[aria-labelledby="' + btnId + '"]');
    if (!button || !menu || !hiddenInput) return null;

    function findItem(value) {
        return Array.from(menu.querySelectorAll('.dropdown-item'))
            .find(i => (i.dataset.value || '') === value);
    }

    function selectItem(item, fireChange) {
        const value = item.dataset.value || '';
        hiddenInput.value = value;
        button.textContent = item.textContent.trim();
        menu.querySelectorAll('.dropdown-item').forEach(i => i.classList.remove('active'));
        item.classList.add('active');
        if (fireChange && typeof options.onChange === 'function') {
            options.onChange(value);
        }
    }

    function bindItem(item) {
        item.addEventListener('click', function (e) {
            e.preventDefault();
            selectItem(item, true);
        });
    }

    menu.querySelectorAll('.dropdown-item').forEach(bindItem);

    return {
        // Programmatically pick an item (e.g. resetting to the placeholder).
        selectByValue: function (value, fireChange) {
            const item = findItem(value);
            if (item) selectItem(item, fireChange);
        },
        // Appends a new option to the menu at runtime (e.g. a fiber plan just
        // added via its modal) and wires its click like every other item.
        addItem: function (value, label) {
            const li = document.createElement('li');
            const a = document.createElement('a');
            a.className = 'dropdown-item';
            a.href = '#';
            a.dataset.value = value;
            a.textContent = label;
            li.appendChild(a);
            menu.appendChild(li);
            bindItem(a);
            return a;
        },
        // Removes an option from the menu (e.g. a deleted fiber plan). If it was
        // the current value, falls back to the placeholder ("") item, if any.
        removeItem: function (value) {
            const item = findItem(value);
            if (!item) return;
            const wasSelected = hiddenInput.value === value;
            item.closest('li').remove();
            if (wasSelected) {
                const placeholder = findItem('');
                if (placeholder) selectItem(placeholder, false);
            }
        }
    };
}
