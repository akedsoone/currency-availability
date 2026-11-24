const chart = document.querySelector('#availability-chart');
const chartData = document.querySelector('#chart-data');

if (chart && chartData) {
    const data = JSON.parse(chartData.textContent);
    const context = chart.getContext('2d');
    const start = Date.parse(data.start);
    const end = Date.parse(data.end);
    const levels = ['Нет', 'Мало', 'Достаточно', 'Много'];
    const colors = { USD: '#219575', EUR: '#8ba81c', PLN: '#507fc1' };
    const color = colors[data.currency] ?? colors.USD;
    const runs = [];
    for (const interval of data.intervals) {
        const sample = { start: Date.parse(interval.start), end: Date.parse(interval.end), level: interval.level };
        const run = runs.at(-1);
        if (run?.at(-1).end === sample.start) {
            run.push(sample);
        } else {
            runs.push([sample]);
        }
    }
    const formatter = new Intl.DateTimeFormat('ru-RU', data.days === 1
        ? { timeZone: 'Europe/Minsk', hour: '2-digit', minute: '2-digit' }
        : { timeZone: 'Europe/Minsk', month: '2-digit', day: '2-digit' });

    function draw() {
        const width = chart.clientWidth;
        const height = chart.clientHeight;
        const ratio = window.devicePixelRatio || 1;
        chart.width = Math.round(width * ratio);
        chart.height = Math.round(height * ratio);
        context.scale(ratio, ratio);

        const left = 66;
        const right = width - 22;
        const top = 14;
        const bottom = height - 27;
        const x = time => left + (time - start) / (end - start) * (right - left);
        const y = level => bottom - level * (bottom - top) / (levels.length - 1);

        context.font = '12px Calibri, system-ui, sans-serif';
        context.fillStyle = '#626b65';
        context.strokeStyle = '#eceeec';
        context.textAlign = 'right';
        context.textBaseline = 'middle';

        levels.forEach((label, level) => {
            context.fillText(label, left - 10, y(level));
            context.beginPath();
            context.moveTo(left, y(level));
            context.lineTo(right, y(level));
            context.stroke();
        });

        const tickCount = width < 470 ? 3 : 5;
        context.textAlign = 'center';
        for (let index = 0; index <= tickCount; index++) {
            const time = start + (end - start) * index / tickCount;
            const position = x(time);
            context.beginPath();
            context.moveTo(position, top);
            context.lineTo(position, bottom);
            context.stroke();
            context.fillText(formatter.format(time), position, bottom + 14);
        }

        const fill = context.createLinearGradient(0, top, 0, bottom);
        fill.addColorStop(0, `${color}38`);
        fill.addColorStop(1, `${color}14`);
        context.fillStyle = fill;
        context.strokeStyle = color;
        context.lineWidth = 2;
        context.lineCap = 'round';

        for (const run of runs) {
            const first = run[0];
            const last = run.at(-1);
            const curve = new Path2D();
            let previous = first;
            curve.moveTo(x(first.start), y(first.level));

            for (const current of run.slice(1)) {
                const widthBefore = (previous.end - previous.start) / 2;
                const widthAfter = (current.end - current.start) / 2;
                const radius = Math.min(widthBefore, widthAfter);
                const boundary = x(current.start);
                curve.lineTo(x(current.start - radius), y(previous.level));
                curve.bezierCurveTo(boundary, y(previous.level), boundary, y(current.level),
                    x(current.start + radius), y(current.level));
                previous = current;
            }

            curve.lineTo(x(last.end), y(last.level));
            const area = new Path2D(curve);
            area.lineTo(x(last.end), bottom);
            area.lineTo(x(first.start), bottom);
            area.closePath();
            context.fill(area);
            context.stroke(curve);
        }
    }

    new ResizeObserver(draw).observe(chart.parentElement);
}
