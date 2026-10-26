const charts = new Map();
const ns = 'http://www.w3.org/2000/svg';
const svgNode = (name, attributes = {}, text = null) => {
    const node = document.createElementNS(ns, name);
    for (const [key, value] of Object.entries(attributes)) node.setAttribute(key, value);
    if (text != null) node.textContent = text;
    return node;
};
const niceStep = (value, roundUp) => {
    const power = 10 ** Math.floor(Math.log10(Math.max(value, .000001)));
    const choices = [1, 2, 2.5, 5, 10];
    return power * (roundUp ? choices.find(n => n >= value / power - 1e-9) : choices.filter(n => n <= value / power + 1e-9).at(-1));
};
export function yAxis(min, max) {
    if (max - min < 1) { min -= 10; max += 10; }
    let step = niceStep((max - min) / 3, true);
    let start = Math.floor(min / step) * step;
    while (start + 3 * step < max - 1e-7) { step = niceStep(step * 1.01, true); start = Math.floor(min / step) * step; }
    return Array.from({ length: 4 }, (_, i) => start + step * i);
}
export function xAxis(total, width, unit) {
    const desired = Math.max(5, Math.min(9, Math.floor(width / 140)));
    const step = niceStep(total / (desired - 1), false);
    const result = [0];
    const gap = Math.min(60, width / 5) / width * total;
    for (let value = step; value < total - gap; value += step) result.push(value);
    result.push(total);
    return result;
}
export function slopeColor(value) {
    const colors = [[5, [76,175,80]], [10, [214,196,58]], [15, [217,111,50]], [20, [139,30,30]]];
    const slope = Math.abs(value);
    if (slope <= 5) return '#4caf50';
    for (let i = 1; i < colors.length; i++) {
        if (slope <= colors[i][0]) {
            const t = (slope - colors[i - 1][0]) / (colors[i][0] - colors[i - 1][0]);
            return '#' + colors[i][1].map((v, c) => Math.round(colors[i - 1][1][c] + (v - colors[i - 1][1][c]) * t).toString(16).padStart(2, '0')).join('');
        }
    }
    return '#8b1e1e';
}

// Monotone cubic interpolation smooths the line without overshooting peaks or troughs.
function smoothPath(points) {
    const slopes = points.slice(1).map((p, i) => (p.y - points[i].y) / Math.max(.000001, p.x - points[i].x));
    const tangents = points.map((p, i) => {
        if (i === 0) return slopes[0];
        if (i === points.length - 1) return slopes.at(-1);
        if (slopes[i - 1] * slopes[i] <= 0) return 0;
        const previous = p.x - points[i - 1].x, next = points[i + 1].x - p.x;
        const w1 = 2 * next + previous, w2 = next + 2 * previous;
        return (w1 + w2) / (w1 / slopes[i - 1] + w2 / slopes[i]);
    });
    let path = `M${points[0].x},${points[0].y}`;
    for (let i = 1; i < points.length; i++) {
        const previous = points[i - 1], point = points[i], dx = (point.x - previous.x) / 3;
        path += `C${previous.x + dx},${previous.y + tangents[i - 1] * dx} ${point.x - dx},${point.y - tangents[i] * dx} ${point.x},${point.y}`;
    }
    return path;
}

export function render(id, profile, options) {
    dispose(id);
    const element = document.getElementById(id);
    if (!element || profile.samples.length < 2) return;
    const svg = svgNode('svg', { 'aria-hidden': true });
    const tooltip = document.createElement('div'); tooltip.className = 'elevation-tooltip'; tooltip.hidden = true;
    element.replaceChildren(svg, tooltip);
    const state = { element, svg, tooltip, profile, options, index: 0 };
    state.resize = new ResizeObserver(() => draw(state)); state.resize.observe(element);
    state.pointer = event => showHover(state, event.clientX - element.getBoundingClientRect().left, event.clientY - element.getBoundingClientRect().top);
    state.leave = () => { tooltip.hidden = true; state.crosshair?.setAttribute('visibility', 'hidden'); };
    state.key = event => {
        if (!['ArrowLeft','ArrowRight','Home','End','Escape'].includes(event.key)) return;
        event.preventDefault();
        if (event.key === 'Escape') { state.leave(); return; }
        const count = profile.samples.length;
        state.index = Math.max(0, Math.min(count - 1, event.key === 'Home' ? 0 : event.key === 'End' ? count - 1 : state.index + (event.key === 'ArrowLeft' ? -1 : 1) * Math.max(1, Math.floor(count / 100))));
        showHover(state, state.toX(profile.samples[state.index].distanceMeters), 80);
    };
    element.addEventListener('pointermove', state.pointer); element.addEventListener('pointerleave', state.leave);
    element.addEventListener('keydown', state.key); element.addEventListener('blur', state.leave);
    charts.set(id, state);
    draw(state);
}

function draw(state) {
    const { element, svg, profile, options } = state;
    const width = element.clientWidth, height = element.clientHeight;
    if (width < 100 || height < 50) return;
    const left = width < 500 ? 48 : 62, right = width - 22, top = 12, bottom = height - 30;
    const altitudeFactor = options.altitudeUnit === 'ft' ? 3.280839895 : 1;
    const distanceFactor = options.distanceUnit === 'mi' ? 1609.344 : 1000;
    const elevations = profile.samples.map(p => p.elevationMeters * altitudeFactor);
    const yTicks = yAxis(Math.min(...elevations), Math.max(...elevations));
    state.toX = d => left + d / profile.distanceMeters * (right - left);
    state.toY = e => bottom - (e * altitudeFactor - yTicks[0]) / (yTicks.at(-1) - yTicks[0]) * (bottom - top);
    state.bounds = { left, right, top, bottom, width, height };
    const format = (number, precision = 2) => new Intl.NumberFormat(options.locale, { maximumFractionDigits: precision }).format(number);
    state.formatDistance = d => `${format(d / distanceFactor, Math.max(2, 1 - Math.floor(Math.log10(Math.max(profile.distanceMeters / distanceFactor, .000001)))))} ${options.distanceUnit}`;
    state.formatAltitude = e => `${format(e * altitudeFactor, 0)} ${options.altitudeUnit}`;
    svg.replaceChildren(); svg.setAttribute('viewBox', `0 0 ${width} ${height}`);
    for (const tick of yTicks) {
        const y = state.toY(tick / altitudeFactor);
        svg.append(svgNode('line', {x1:left,y1:y,x2:right,y2:y,class:'elevation-grid'}));
        svg.append(svgNode('text', {x:left-10,y,dy:'.35em','text-anchor':'end',class:'elevation-axis-label'}, `${format(tick, 0)} ${options.altitudeUnit}`));
    }
    const xTicks = xAxis(profile.distanceMeters / distanceFactor, right - left, options.distanceUnit);
    for (const tick of xTicks) {
        const x = state.toX(tick * distanceFactor);
        svg.append(svgNode('line', {x1:x,y1:top,x2:x,y2:bottom,class:'elevation-grid'}));
        svg.append(svgNode('text', {x,y:bottom+20,'text-anchor':tick === 0 ? 'start' : tick === xTicks.at(-1) ? 'end' : 'middle',class:'elevation-axis-label'}, state.formatDistance(tick * distanceFactor)));
    }
    const gradientId = `${element.id}-color`;
    const gradient = svgNode('linearGradient', {id:gradientId,x1:0,x2:1,y1:0,y2:0});
    const stride = Math.max(1, Math.ceil(profile.samples.length / 1000));
    const plotted = profile.samples.filter((_, i) => i % stride === 0 || i === profile.samples.length - 1);
    for (const point of plotted) gradient.append(svgNode('stop', {offset:point.distanceMeters/profile.distanceMeters,'stop-color':slopeColor(point.slopePercent)}));
    const defs = svgNode('defs'); defs.append(gradient); svg.append(defs);
    const points = plotted.map(p => ({ x:state.toX(p.distanceMeters), y:state.toY(p.elevationMeters) }));
    const path = smoothPath(points);
    svg.append(svgNode('path', {d:`${path}L${right},${bottom}L${left},${bottom}Z`,fill:`url(#${gradientId})`,'fill-opacity':.18}));
    svg.append(svgNode('path', {d:path,fill:'none',stroke:`url(#${gradientId})`,'stroke-width':2.5,'stroke-linecap':'round','stroke-linejoin':'round'}));
    svg.append(svgNode('path', {d:`M${left},${top}V${bottom}H${right}`,class:'elevation-axes'}));
    state.crosshair = svgNode('g', {visibility:'hidden',class:'elevation-crosshair'});
    state.guide = svgNode('line', {y1:top,y2:bottom}); state.dot = svgNode('circle', {r:4});
    state.crosshair.append(state.guide,state.dot); svg.append(state.crosshair);
    state.tooltip.hidden = true;
}

function showHover(state, pointerX, pointerY) {
    if (!state.bounds) return;
    const { profile, options, tooltip, bounds } = state;
    const x = Math.max(bounds.left, Math.min(bounds.right, pointerX));
    const distance = (x - bounds.left) / (bounds.right - bounds.left) * profile.distanceMeters;
    let low = 0, high = profile.samples.length - 1;
    while (high - low > 1) { const mid = (low + high) >> 1; if (profile.samples[mid].distanceMeters <= distance) low = mid; else high = mid; }
    const a = profile.samples[low], b = profile.samples[high];
    const t = b.distanceMeters > a.distanceMeters ? (distance - a.distanceMeters) / (b.distanceMeters - a.distanceMeters) : 0;
    const elevation = a.elevationMeters + t * (b.elevationMeters - a.elevationMeters), slope = a.slopePercent + t * (b.slopePercent - a.slopePercent);
    state.index = t < .5 ? low : high;
    state.guide.setAttribute('x1',x); state.guide.setAttribute('x2',x);
    state.dot.setAttribute('cx',x); state.dot.setAttribute('cy',state.toY(elevation)); state.dot.setAttribute('fill',slopeColor(slope));
    state.crosshair.setAttribute('visibility','visible');
    const minutes = Math.round(distance / 1000 / options.speedKph * 60);
    const values = [[options.distanceLabel,state.formatDistance(distance)],[options.timeLabel,`${Math.floor(minutes/60)}h ${String(minutes%60).padStart(2,'0')}m`],[options.altitudeLabel,state.formatAltitude(elevation)],[options.slopeLabel,`${Math.round(slope)}%`]];
    tooltip.replaceChildren();
    for (const [label,value] of values) { const row=document.createElement('div'), title=document.createElement('span'), data=document.createElement('strong'); title.textContent=label; data.textContent=value; row.append(title,data); tooltip.append(row); }
    tooltip.hidden=false;
    tooltip.style.left=`${Math.max(4,Math.min(bounds.width-tooltip.offsetWidth-4,pointerX+14))}px`;
    tooltip.style.top=`${Math.max(4,Math.min(bounds.height-tooltip.offsetHeight-4,pointerY-tooltip.offsetHeight-12))}px`;
}

export function dispose(id) {
    const state=charts.get(id); if (!state) return;
    state.resize.disconnect(); state.element.removeEventListener('pointermove',state.pointer); state.element.removeEventListener('pointerleave',state.leave);
    state.element.removeEventListener('keydown',state.key); state.element.removeEventListener('blur',state.leave); charts.delete(id);
}
