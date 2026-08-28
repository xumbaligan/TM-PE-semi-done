// Off-canvas sidebar toggle, shared by every layout (_Layout, _Admin,
// _FieldTechnicianLayout, _OfficeStaffLayout). Below 992px the sidebar starts
// translated off-screen (see site.css); this just adds/removes the
// .sidebar-open class and a dimmed backdrop. At 992px+ the CSS media query
// that makes those classes matter doesn't apply, so this is a no-op there.
(function () {
    function init() {
        const sidebar = document.querySelector('.sidebar');
        const backdrop = document.querySelector('.sidebar-backdrop');
        const toggleButtons = document.querySelectorAll('[data-sidebar-toggle]');
        if (!sidebar || toggleButtons.length === 0) return;

        function open() {
            sidebar.classList.add('sidebar-open');
            if (backdrop) backdrop.classList.add('show');
        }

        function close() {
            sidebar.classList.remove('sidebar-open');
            if (backdrop) backdrop.classList.remove('show');
        }

        toggleButtons.forEach(function (btn) {
            btn.addEventListener('click', function () {
                if (sidebar.classList.contains('sidebar-open')) close(); else open();
            });
        });

        if (backdrop) backdrop.addEventListener('click', close);

        // Tapping a nav link navigates to a new page anyway, but closing
        // first avoids a visible flash of the open panel while it unloads.
        sidebar.querySelectorAll('a.nav-link').forEach(function (link) {
            link.addEventListener('click', close);
        });

        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') close();
        });
    }

    document.addEventListener('DOMContentLoaded', init);
})();
