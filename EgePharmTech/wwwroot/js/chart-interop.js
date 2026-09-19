// Chart.js interop for Blazor (Compatible with Chart.js 2.9.4)
let chartInstances = {};

// Ensure Unicode/Turkish glyphs render correctly (Chart.js v2 uses this default)
if (typeof Chart !== 'undefined' && Chart.defaults && Chart.defaults.global) {
    Chart.defaults.global.defaultFontFamily =
        "'Noto Sans', Arial, 'Segoe UI', Tahoma, sans-serif";
    Chart.defaults.global.defaultFontSize = 12;
    Chart.defaults.global.defaultFontColor = '#666';
}

function applyFontOptions(options) {
    options = options || {};
    options.defaultFontFamily = "system-ui, -apple-system, 'Segoe UI', Roboto, Arial, 'Noto Sans', 'DejaVu Sans', sans-serif";
    return options;
}

window.chartHelper = window.chartHelper || {};

Object.assign(window.chartHelper, {
    // Comparison Chart: Gerçek vs Tahmin (zaman ekseninde)
    createComparisonChart: function (canvasId, timeLabels, actualData, predictedData, title) {
        const ctx = document.getElementById(canvasId);
        if (!ctx) {
            console.error('Canvas not found:', canvasId);
            return;
        }

        if (chartInstances[canvasId]) {
            chartInstances[canvasId].destroy();
        }

        chartInstances[canvasId] = new Chart(ctx, {
            type: 'line',
            data: {
                labels: timeLabels,
                datasets: [
                    {
                        label: 'Gerçek Invivo',
                        data: actualData,
                        borderColor: 'rgb(54, 162, 235)',
                        backgroundColor: 'rgba(54, 162, 235, 0.5)',
                        pointRadius: 5,
                        pointHoverRadius: 7,
                        lineTension: 0.1,
                        fill: false
                    },
                    {
                        label: 'Tahmin Edilen Invivo',
                        data: predictedData,
                        borderColor: 'rgb(255, 99, 132)',
                        backgroundColor: 'rgba(255, 99, 132, 0.5)',
                        pointRadius: 5,
                        pointHoverRadius: 7,
                        lineTension: 0.1,
                        fill: false
                    }
                ]
            },
            options: {
                responsive: true,
                title: {
                    display: true,
                    text: title || 'Gerçek vs Tahmin Karşılaştırması'
                },
                legend: {
                    position: 'top'
                },
                scales: {
                    xAxes: [{
                        scaleLabel: {
                            display: true,
                            labelString: 'Zaman'
                        }
                    }],
                    yAxes: [{
                        scaleLabel: {
                            display: true,
                            labelString: 'Yüzde (%)'
                        },
                        ticks: {
                            min: 0,
                            max: 100
                        }
                    }]
                }
            }
        });
    },

    // Correlation Chart: Scatter plot + trend line
    createCorrelationChart: function (canvasId, xData, yData, trendLineData, xLabel, yLabel, title) {
        const ctx = document.getElementById(canvasId);
        if (!ctx) {
            console.error('Canvas not found:', canvasId);
            return;
        }

        if (chartInstances[canvasId]) {
            chartInstances[canvasId].destroy();
        }

        const scatterData = xData.map((x, i) => ({ x: x, y: yData[i] }));
        const trendData = trendLineData.map((point) => ({ x: point.x, y: point.y }));

        chartInstances[canvasId] = new Chart(ctx, {
            type: 'scatter',
            data: {
                datasets: [
                    {
                        label: 'Veri Noktaları',
                        data: scatterData,
                        backgroundColor: 'rgb(54, 162, 235)',
                        pointRadius: 6,
                        pointHoverRadius: 8
                    },
                    {
                        label: 'Trend Çizgisi',
                        data: trendData,
                        type: 'line',
                        borderColor: 'rgb(255, 99, 132)',
                        backgroundColor: 'transparent',
                        pointRadius: 0,
                        borderWidth: 2,
                        lineTension: 0,
                        fill: false
                    }
                ]
            },
            options: {
                responsive: true,
                title: {
                    display: true,
                    text: title || 'Korelasyon Grafiği'
                },
                legend: {
                    position: 'top'
                },
                scales: {
                    xAxes: [{
                        type: 'linear',
                        position: 'bottom',
                        scaleLabel: {
                            display: true,
                            labelString: xLabel || 'X'
                        }
                    }],
                    yAxes: [{
                        scaleLabel: {
                            display: true,
                            labelString: yLabel || 'Y'
                        }
                    }]
                }
            }
        });
    },

    // Release Chart: Zaman-Salım grafiği (XY Scatter with line).
    // labels: arayüz dilinden gelen etiketler {observed, predicted, predictedPrefix, x, y};
    // verilmezse Türkçe varsayılanlar kullanılır (maege.tr tek dilli).
    createReleaseChart: function (canvasId, timeLabels, releaseData, title, predictedData, modelName, labels) {
        const L = Object.assign({
            observed: 'Gözlenen Salım (%)',
            predictedPrefix: 'Tahmini',
            predicted: 'Tahmini Salım (%)',
            x: 'Zaman',
            y: 'Salım (%)'
        }, labels || {});
        const ctx = document.getElementById(canvasId);
        if (!ctx) {
            console.error('Canvas not found:', canvasId);
            return;
        }

        if (chartInstances[canvasId]) {
            chartInstances[canvasId].destroy();
        }

        const scatterData = timeLabels.map((t, i) => ({
            x: parseFloat(t),
            y: releaseData[i]
        }));

        const datasets = [
            {
                label: L.observed,
                data: scatterData,
                backgroundColor: 'rgb(54, 162, 235)',
                borderColor: 'rgb(54, 162, 235)',
                pointRadius: 6,
                pointHoverRadius: 8,
                showLine: true,
                lineTension: 0.1,
                fill: false
            }
        ];

        if (predictedData && predictedData.length > 0) {
            const predictedScatterData = timeLabels.map((t, i) => ({
                x: parseFloat(t),
                y: predictedData[i]
            }));
            datasets.push({
                label: modelName ? `${L.predictedPrefix} (${modelName})` : L.predicted,
                data: predictedScatterData,
                backgroundColor: 'rgb(255, 99, 132)',
                borderColor: 'rgb(255, 99, 132)',
                pointRadius: 4,
                pointHoverRadius: 6,
                showLine: true,
                lineTension: 0.3,
                fill: false,
                borderDash: [5, 5]
            });
        }

        chartInstances[canvasId] = new Chart(ctx, {
            type: 'scatter',
            data: {
                datasets: datasets
            },
            options: {
                responsive: true,
                title: {
                    display: true,
                    text: title || 'Zaman - Salım Profili',
                    fontSize: 14
                },
                legend: {
                    display: predictedData && predictedData.length > 0,
                    position: 'top'
                },
                scales: {
                    xAxes: [{
                        type: 'linear',
                        position: 'bottom',
                        scaleLabel: {
                            display: true,
                            labelString: L.x
                        },
                        ticks: {
                            min: 0
                        }
                    }],
                    yAxes: [{
                        scaleLabel: {
                            display: true,
                            labelString: L.y
                        },
                        ticks: {
                            min: 0,
                            max: 100
                        }
                    }]
                }
            }
        });
    },

    // XY Scatter Comparison: Reference vs Product. labels: {reference, product, x, y} (arayüz dili);
    // verilmezse önceki İngilizce sabitler kullanılır.
    createReleaseComparisonChart: function (canvasId, timeLabels, referenceData, productData, title, labels) {
        const L = Object.assign({
            reference: 'Reference (%)',
            product: 'Product (%)',
            x: 'Time (h)',
            y: 'Release (%)'
        }, labels || {});
        const ctx = document.getElementById(canvasId);
        if (!ctx) {
            console.error('Canvas not found:', canvasId);
            return;
        }

        if (chartInstances[canvasId]) {
            chartInstances[canvasId].destroy();
        }

        const refScatterData = timeLabels.map((t, i) => ({
            x: parseFloat(t),
            y: referenceData && referenceData.length > i ? referenceData[i] : null
        }));

        const prodScatterData = timeLabels.map((t, i) => ({
            x: parseFloat(t),
            y: productData && productData.length > i ? productData[i] : null
        }));

        chartInstances[canvasId] = new Chart(ctx, {
            type: 'scatter',
            data: {
                datasets: [
                    {
                        label: L.reference,
                        data: refScatterData,
                        backgroundColor: 'rgb(54, 162, 235)',
                        borderColor: 'rgb(54, 162, 235)',
                        pointRadius: 6,
                        pointHoverRadius: 8,
                        showLine: true,
                        lineTension: 0.1,
                        fill: false
                    },
                    {
                        label: L.product,
                        data: prodScatterData,
                        backgroundColor: 'rgb(255, 99, 132)',
                        borderColor: 'rgb(255, 99, 132)',
                        pointRadius: 6,
                        pointHoverRadius: 8,
                        showLine: true,
                        lineTension: 0.1,
                        fill: false
                    }
                ]
            },
            options: {
                responsive: true,
                title: {
                    display: true,
                    text: title || 'Reference vs Product (XY Scatter)',
                    fontSize: 14
                },
                legend: {
                    position: 'top'
                },
                scales: {
                    xAxes: [{
                        type: 'linear',
                        position: 'bottom',
                        scaleLabel: {
                            display: true,
                            labelString: L.x
                        },
                        ticks: {
                            min: 0
                        }
                    }],
                    yAxes: [{
                        scaleLabel: {
                            display: true,
                            labelString: L.y
                        },
                        ticks: {
                            min: 0,
                            max: 100
                        }
                    }]
                }
            }
        });
    },

    destroyChart: function (canvasId) {
        if (chartInstances[canvasId]) {
            chartInstances[canvasId].destroy();
            delete chartInstances[canvasId];
        }
    }
});

// Generic chart interop for flexible chart rendering
window.chartInterop = window.chartInterop || {};

Object.assign(window.chartInterop, {
    renderChart: function (canvasId, config) {
        const ctx = document.getElementById(canvasId);
        if (!ctx) {
            console.error('Canvas not found:', canvasId);
            return;
        }

        // Destroy existing chart if present
        if (chartInstances[canvasId]) {
            chartInstances[canvasId].destroy();
        }

        try {
            chartInstances[canvasId] = new Chart(ctx, config);
        } catch (error) {
            console.error('Error creating chart:', error);
        }
    },

    destroyChart: function (canvasId) {
        if (chartInstances[canvasId]) {
            chartInstances[canvasId].destroy();
            delete chartInstances[canvasId];
        }
    }
});

