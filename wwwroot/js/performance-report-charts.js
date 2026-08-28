// Charts for the Performance Report.
//
// The data comes from the <script type="application/json" id="reportChartData">
// block the page renders, so nothing here depends on Razor interpolating values
// into JavaScript.
//
// Every failure path shows a message inside the chart card instead of leaving a
// blank canvas: an empty report, a blocked Chart.js CDN, and a bad payload all
// say so plainly.
(function () {
    const dataEl = document.getElementById('reportChartData');
    if (!dataEl) return;

    function showMessage(id, text) {
        const el = document.getElementById(id);
        if (!el) return;
        el.textContent = text;
        el.classList.remove('d-none');

        // Hide the empty canvas so the card doesn't show a blank box next to
        // the explanation.
        const canvas = el.parentElement.querySelector('canvas');
        if (canvas) canvas.classList.add('d-none');
    }

    let payload;
    try {
        payload = JSON.parse(dataEl.textContent);
    } catch (err) {
        console.error('Performance Report: could not parse chart data.', err);
        showMessage('scoreByEmployeeMessage', 'Chart data could not be read.');
        showMessage('ratingDistributionMessage', 'Chart data could not be read.');
        return;
    }

    const scoreData = payload.scoreByEmployee || [];
    const ratingData = payload.ratingDistribution || [];

    // Chart.js is loaded from a CDN, so an offline machine or a blocked network
    // shows up here rather than as two empty boxes.
    if (typeof Chart === 'undefined') {
        console.error('Performance Report: Chart.js failed to load (CDN blocked or offline).');
        const msg = 'Charts could not load. Check your internet connection and refresh.';
        showMessage('scoreByEmployeeMessage', msg);
        showMessage('ratingDistributionMessage', msg);
        return;
    }

    const emptyMsg = 'No evaluations match the current filters, so there is nothing to chart yet.';

    // Same colours the rating badges (and the pie chart below) use, so every
    // view of a rating reads as the same thing.
    const ratingColors = {
        'Excellent': '#198754',
        'Very Good': '#0d6efd',
        'Good': '#0dcaf0',
        'Needs Improvement': '#ffc107',
        'Poor': '#dc3545'
    };
    const unevaluatedColor = '#adb5bd';

    // ---- Bar chart: average score per employee ----
    if (scoreData.length === 0) {
        showMessage('scoreByEmployeeMessage', emptyMsg);
    } else {
        // A category scale spaces its bars evenly across the full chart
        // width no matter how many there are, so with just a few employees
        // the bars end up spread out (reading as centered) instead of
        // sitting together at the start. Padding out extra, unlabeled,
        // valueless categories after the real ones keeps each bar at its
        // normal width and packs the real employees left-to-right, while
        // the chart itself stays exactly the size it was.
        const canvas = document.getElementById('scoreByEmployeeChart');
        const perBar = 90;
        const availableWidth = canvas.parentElement.clientWidth;
        const totalSlots = Math.max(scoreData.length, Math.floor(availableWidth / perBar));

        const labels = scoreData.map(d => d.Label);
        // An unevaluated employee still gets a real (zero-height) bar and a
        // tooltip - null is reserved for the unlabeled padding slots below,
        // which must stay invisible and untouchable.
        const values = scoreData.map(d => d.Value === null || d.Value === undefined ? 0 : Number(d.Value));
        const ratings = scoreData.map(d => d.Rating || 'Not yet evaluated');
        const colors = scoreData.map(d => (d.Value === null || d.Value === undefined)
            ? unevaluatedColor
            : (ratingColors[d.Rating] || unevaluatedColor));

        for (let i = scoreData.length; i < totalSlots; i++) {
            labels.push('');
            values.push(null);
            ratings.push('');
            colors.push('transparent');
        }

        new Chart(canvas, {
            type: 'bar',
            data: {
                labels: labels,
                datasets: [{
                    label: 'Average Score',
                    data: values,
                    backgroundColor: colors,
                    borderRadius: 6,
                    maxBarThickness: 48
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: { display: false },
                    tooltip: {
                        callbacks: {
                            label: function (ctx) {
                                const rating = ratings[ctx.dataIndex];
                                if (!rating) return '';
                                return rating === 'Not yet evaluated'
                                    ? 'Not yet evaluated'
                                    : ctx.parsed.y + ' / 4 (' + rating + ')';
                            }
                        }
                    }
                },
                scales: {
                    y: {
                        beginAtZero: true,
                        max: 4,
                        title: { display: true, text: 'Score out of 4' }
                    },
                    x: {
                        ticks: { autoSkip: false, maxRotation: 45, minRotation: 0 }
                    }
                }
            }
        });
    }

    // ---- Pie chart: rating distribution ----
    if (ratingData.length === 0) {
        showMessage('ratingDistributionMessage', emptyMsg);
    } else {
        new Chart(document.getElementById('ratingDistributionChart'), {
            type: 'pie',
            data: {
                labels: ratingData.map(d => d.Label),
                datasets: [{
                    data: ratingData.map(d => Number(d.Value)),
                    backgroundColor: ratingData.map(d => ratingColors[d.Label] || '#6c757d'),
                    borderColor: '#fff',
                    borderWidth: 2
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    // Percentages sit right on the legend (not just the hover
                    // tooltip) so a manager can read the ratings breakdown at
                    // a glance, including on a printed/exported view of the page.
                    legend: {
                        position: 'bottom',
                        labels: {
                            generateLabels: function (chart) {
                                const data = chart.data;
                                const values = data.datasets[0].data;
                                const total = values.reduce((a, b) => a + b, 0);
                                return data.labels.map(function (label, i) {
                                    const pct = total ? Math.round(values[i] * 100 / total) : 0;
                                    return {
                                        text: label + ' — ' + pct + '%',
                                        fillStyle: data.datasets[0].backgroundColor[i],
                                        strokeStyle: data.datasets[0].borderColor,
                                        lineWidth: data.datasets[0].borderWidth,
                                        index: i
                                    };
                                });
                            }
                        }
                    },
                    tooltip: {
                        callbacks: {
                            label: function (ctx) {
                                const total = ctx.dataset.data.reduce((a, b) => a + b, 0);
                                const pct = total ? Math.round(ctx.parsed * 100 / total) : 0;
                                const noun = ctx.parsed === 1 ? 'employee' : 'employees';
                                return ctx.label + ': ' + ctx.parsed + ' ' + noun + ' (' + pct + '%)';
                            }
                        }
                    }
                }
            }
        });
    }
})();
