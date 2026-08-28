// Fills the shared _EvaluationModal partial from the data-* attributes on any
// .view-evaluation-btn trigger. Depends on star-rating.js for star markup.
(function () {
    function escapeHtml(value) {
        return String(value ?? '').replace(/[&<>"']/g, c => ({
            '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
        }[c]));
    }

    function ratingBadgeClass(rating) {
        switch (rating) {
            case 'Excellent': return 'bg-success-subtle text-success-emphasis';
            case 'Very Good': return 'bg-primary-subtle text-primary-emphasis';
            case 'Good': return 'bg-info-subtle text-info-emphasis';
            case 'Needs Improvement': return 'bg-warning-subtle text-warning-emphasis';
            case 'Poor': return 'bg-danger-subtle text-danger-emphasis';
            default: return 'bg-secondary-subtle text-secondary-emphasis';
        }
    }

    function parseJson(raw) {
        try { return JSON.parse(raw || '[]'); } catch (e) { return []; }
    }

    function fill(btn) {
        const d = btn.dataset;
        const set = (id, text) => { const el = document.getElementById(id); if (el) el.textContent = text; };
        const setHtml = (id, html) => { const el = document.getElementById(id); if (el) el.innerHTML = html; };

        set('evalEmployee', d.employee || '-');
        set('evalRole', d.role || '-');
        set('evalPeriod', d.period || '-');
        set('evalDate', d.date || '-');
        set('evalEvaluator', d.evaluator || '-');
        set('evalScore', (d.overallScore || '0') + ' / 4');
        setHtml('evalStarsTop', window.StarRating.render(parseFloat(d.overallScore || '0'), ''));

        const ratingBadge = document.getElementById('evalRatingBadge');
        if (ratingBadge) {
            ratingBadge.textContent = d.rating || '-';
            ratingBadge.className = 'badge ' + ratingBadgeClass(d.rating);
        }

        const statusBadge = document.getElementById('evalStatusBadge');
        if (statusBadge) {
            statusBadge.textContent = d.status || '-';
            statusBadge.className = 'badge ' + (d.status === 'Finalized'
                ? 'bg-success-subtle text-success-emphasis'
                : 'bg-secondary-subtle text-secondary-emphasis');
        }

        // ---- Performance snapshot tiles ----
        set('evalStatCompleted', d.completed || '0');
        set('evalStatOnTime', (d.ontime || '0') + '%');

        const tile3Icon = document.getElementById('evalStatTile3Icon');
        const tile3Label = document.getElementById('evalStatTile3Label');
        const tile3Value = document.getElementById('evalStatTile3Value');
        const tile4Icon = document.getElementById('evalStatTile4Icon');
        const tile4Label = document.getElementById('evalStatTile4Label');
        const tile4Value = document.getElementById('evalStatTile4Value');

        // Tile 3/4 show different metrics depending on role, same as the New
        // Evaluation page: job ticket Rescheduled/Cancelled counts for a Field
        // Technician (Office Tasks never use those statuses), Rejected
        // activities/Overdue tasks for an Office Staff.
        if (tile3Icon && tile3Label && tile3Value && tile4Icon && tile4Label && tile4Value) {
            if (d.roleType === 'OfficeStaff') {
                tile3Icon.className = 'stat-icon bg-danger-subtle text-danger';
                tile3Icon.innerHTML = '<i class="bi bi-hand-thumbs-down"></i>';
                tile3Label.textContent = 'Rejected Activities';
                tile3Value.textContent = d.rejected || '0';

                tile4Icon.className = 'stat-icon bg-warning-subtle text-warning';
                tile4Icon.innerHTML = '<i class="bi bi-exclamation-triangle"></i>';
                tile4Label.textContent = 'Overdue Task';
                tile4Value.textContent = d.overdue || '0';
            } else {
                tile3Icon.className = 'stat-icon bg-warning-subtle text-warning';
                tile3Icon.innerHTML = '<i class="bi bi-arrow-repeat"></i>';
                tile3Label.textContent = 'Rescheduled';
                tile3Value.textContent = d.rescheduled || '0';

                tile4Icon.className = 'stat-icon bg-danger-subtle text-danger';
                tile4Icon.innerHTML = '<i class="bi bi-x-circle"></i>';
                tile4Label.textContent = 'Cancelled';
                tile4Value.textContent = d.cancelled || '0';
            }
        }

        // ---- Criteria ratings ----
        const results = parseJson(d.results);
        const body = document.getElementById('evalResultsBody');
        if (body) {
            if (results.length === 0) {
                body.innerHTML = '<tr><td colspan="4" class="text-muted small">No scored criteria.</td></tr>';
            } else {
                body.innerHTML = results.map(r => `
                    <tr>
                        <td class="small">${escapeHtml(r.CriteriaName)}<div class="text-muted" style="font-size:.75rem">Weight ${r.Weight}%</div></td>
                        <td>${window.StarRating.render(r.Stars)}<div class="text-muted" style="font-size:.72rem">${r.Stars} / 4</div></td>
                        <td class="small">${r.Score} / ${r.Weight}</td>
                        <td class="small">${r.Feedback ? escapeHtml(r.Feedback) : '<span class="text-muted">-</span>'}</td>
                    </tr>`).join('');
            }
        }

        const feedback = document.getElementById('evalFeedback');
        if (feedback) {
            feedback.textContent = d.feedback && d.feedback.trim() ? d.feedback : 'No feedback recorded.';
        }
    }

    document.addEventListener('click', function (e) {
        const btn = e.target.closest('.view-evaluation-btn');
        if (btn) fill(btn);
    });
})();
