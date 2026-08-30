// Charts for the Manager Dashboard (Pages/Index): Workload Distribution
// (ranked bar) and Score Trend Across Periods (line). Data comes from the
// <script type="application/json"> blocks the page renders, same convention
// as performance-report-charts.js - every failure path shows a message
// inside the chart card instead of leaving a blank canvas.
(function () {
    function showMessage(id, text) {
        const el = document.getElementById(id);
        if (!el) return;
        el.textContent = text;
        el.classList.remove('d-none');

        const canvas = el.parentElement.querySelector('canvas');
        if (canvas) canvas.classList.add('d-none');
    }

    function parsePayload(dataElId, messageId) {
        const dataEl = document.getElementById(dataElId);
        if (!dataEl) return null;

        try {
            return JSON.parse(dataEl.textContent);
        } catch (err) {
            console.error('Dashboard: could not parse chart data (' + dataElId + ').', err);
            showMessage(messageId, 'Chart data could not be read.');
            return null;
        }
    }

    const emptyMsg = 'Nothing recorded yet, so there is nothing to chart.';

    if (typeof Chart === 'undefined') {
        console.error('Dashboard: Chart.js failed to load (CDN blocked or offline).');
        const msg = 'Charts could not load. Check your internet connection and refresh.';
        ['workloadDistributionMessage', 'scoreTrendMessage'].forEach(id => showMessage(id, msg));
        return;
    }

    // ---- Workload Distribution: ranked bar, banded Heavy/Normal/Idle ----
    const workload = parsePayload('workloadChartData', 'workloadDistributionMessage');
    if (workload) {
        if (workload.length === 0) {
            showMessage('workloadDistributionMessage', emptyMsg);
        } else {
            // Purple brand family (matches the role-badge shading used
            // elsewhere: darker = heavier) instead of Bootstrap's red/gray/
            // yellow, so the bar colors stay on-palette with the rest of the app.
            const bandColors = { Heavy: '#47076f', Normal: '#9f4cd6', Idle: '#cca8e3' };

            new Chart(document.getElementById('workloadDistributionChart'), {
                type: 'bar',
                data: {
                    labels: workload.map(w => w.Name),
                    datasets: [{
                        label: 'Workload points',
                        data: workload.map(w => Number(w.Points)),
                        backgroundColor: workload.map(w => bandColors[w.Band] || '#9f4cd6'),
                        borderRadius: 4,
                        maxBarThickness: 18
                    }]
                },
                options: {
                    indexAxis: 'y',
                    responsive: true,
                    maintainAspectRatio: false,
                    plugins: {
                        legend: { display: false },
                        tooltip: {
                            callbacks: {
                                label: function (ctx) {
                                    const w = workload[ctx.dataIndex];
                                    return w.Role + ' — ' + w.Points + ' pts (' + w.Band + ')';
                                }
                            }
                        }
                    },
                    scales: {
                        x: { beginAtZero: true, ticks: { precision: 0 } }
                    }
                }
            });
        }
    }

    // ---- Score Trend Across Periods ----
    const trendPayload = parsePayload('scoreTrendData', 'scoreTrendMessage');
    if (trendPayload) {
        if (!trendPayload.hasData) {
            showMessage('scoreTrendMessage', emptyMsg);
        } else {
            const points = trendPayload.points || [];

            new Chart(document.getElementById('scoreTrendChart'), {
                type: 'line',
                data: {
                    labels: points.map(p => p.Label),
                    datasets: [{
                        label: 'Average score',
                        data: points.map(p => Number(p.Value)),
                        borderColor: '#47076f',
                        backgroundColor: 'rgba(71, 7, 111, 0.12)',
                        fill: true,
                        tension: 0.3,
                        pointRadius: 4,
                        pointBackgroundColor: '#47076f'
                    }]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    plugins: { legend: { display: false } },
                    scales: {
                        y: { beginAtZero: true, max: 4, ticks: { stepSize: 1 } }
                    }
                }
            });
        }
    }
})();
