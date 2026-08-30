// Drives the notification bell in the topbar (see Pages/Shared/_NotificationBell.cshtml,
// which server-renders the current badge count/list so there's nothing empty
// to flash on a full page navigation). From there, everything here is AJAX
// against Pages/Notifications/Index.cshtml's handlers - the badge count and
// dropdown list update on a timer and right after marking something read,
// without ever reloading or navigating the page.
(function () {
    const POLL_INTERVAL_MS = 20000;

    const bellBtn = document.getElementById('notifBellBtn');
    if (!bellBtn) return;

    const badge = document.getElementById('notifBadge');
    const list = document.getElementById('notifDropdownList');
    const markAllBtn = document.getElementById('notifMarkAllBtn');
    const tokenInput = document.querySelector('#notifAntiForgeryForm input[name="__RequestVerificationToken"]');

    function post(handler, body) {
        const form = new FormData();
        if (tokenInput) form.append('__RequestVerificationToken', tokenInput.value);
        if (body) Object.entries(body).forEach(([k, v]) => form.append(k, v));
        return fetch('/Notifications?handler=' + handler, { method: 'POST', body: form });
    }

    function escapeHtml(value) {
        return String(value ?? '').replace(/[&<>"']/g, c => ({
            '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
        }[c]));
    }

    function bindItemClicks() {
        list.querySelectorAll('.notif-dropdown-item').forEach(function (item) {
            item.addEventListener('click', function () {
                const id = item.dataset.notifId;
                if (id) post('MarkRead', { id: id });
            });
        });
    }

    function render(items) {
        if (!items.length) {
            list.innerHTML = '<div class="notif-empty">No notifications yet.</div>';
            return;
        }

        list.innerHTML = items.map(function (n) {
            const href = n.url ? n.url : '#';
            return `<a href="${href}" class="notif-dropdown-item ${n.isRead ? '' : 'notif-dropdown-item-unread'}" data-notif-id="${n.id}">
                <div class="notif-dropdown-item-icon"><i class="bi ${escapeHtml(n.icon)}"></i></div>
                <div class="overflow-hidden">
                    <div class="notif-dropdown-item-message">${escapeHtml(n.message)}</div>
                    <div class="notif-dropdown-item-date">${escapeHtml(n.dateCreated)}</div>
                </div>
            </a>`;
        }).join('');

        bindItemClicks();
    }

    async function poll() {
        try {
            const response = await fetch('/Notifications?handler=Poll', { headers: { 'X-Requested-With': 'XMLHttpRequest' } });
            if (!response.ok) return;
            const data = await response.json();

            if (data.unreadCount > 0) {
                badge.textContent = data.unreadCount > 99 ? '99+' : data.unreadCount;
                badge.classList.remove('d-none');
            } else {
                badge.classList.add('d-none');
            }

            render(data.items || []);
        } catch (e) {
            // Offline / blocked network - leave whatever was last shown.
        }
    }

    if (markAllBtn) {
        markAllBtn.addEventListener('click', async function (e) {
            e.stopPropagation();
            await post('MarkAllRead');
            poll();
        });
    }

    // The bell is server-rendered with the current badge count/list already
    // filled in (see _NotificationBell.cshtml), so there's nothing empty to
    // flash while this first poll is in flight - just wire up clicks on
    // what's already on the page, then quietly keep it fresh from here.
    bindItemClicks();
    poll();
    setInterval(poll, POLL_INTERVAL_MS);
})();
