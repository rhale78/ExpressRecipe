// Price chart JS helpers — used by PriceHistoryChart.razor via IJSRuntime
(function () {
    var _charts = {};

    window.renderChart = function (canvasId, config) {
        var existing = _charts[canvasId];
        if (existing) {
            existing.destroy();
            delete _charts[canvasId];
        }
        var canvas = document.getElementById(canvasId);
        if (!canvas) { return; }
        _charts[canvasId] = new Chart(canvas, config);
    };

    window.destroyChart = function (canvasId) {
        var existing = _charts[canvasId];
        if (existing) {
            existing.destroy();
            delete _charts[canvasId];
        }
    };
}());
