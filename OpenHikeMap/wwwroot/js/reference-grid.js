import { utmProjection, metricGridStep } from './utm-grid.js';
const svgNamespace = 'http://www.w3.org/2000/svg';
const element = (name, attributes = {}) => {
    const node = document.createElementNS(svgNamespace, name);
    for (const [key, value] of Object.entries(attributes)) node.setAttribute(key, value);
    return node;
};

export class ReferenceGrid {
    constructor(map, visibleBounds = () => ({ left: 0, right: map.getSize().x, height: map.getSize().y })) {
        this.map = map; this.visibleBounds = visibleBounds;
        this.options = { cardinals: ['N', 'E', 'S', 'W'], locale: 'en-US', stepLabel: 'Spacing', zoomIn: 'Zoom in to show the UTM grid', unavailable: 'UTM: 80°S–84°N' };
        const pane = map.getPane('referenceGrid') || map.createPane('referenceGrid');
        pane.style.zIndex = '350'; pane.style.pointerEvents = 'none';
        this.svg = element('svg', { 'aria-hidden': 'true', focusable: 'false' }); this.svg.classList.add('reference-grid', 'leaflet-zoom-hide'); pane.append(this.svg);
        this.schedule = () => { if (!this.frame) this.frame = requestAnimationFrame(() => { this.frame = 0; this.draw(); }); };
        map.on('move zoom resize', this.schedule); this.draw();
    }
    setOptions(options) { Object.assign(this.options, options); this.schedule(); }
    draw() {
        const map = this.map, size = map.getSize(); if (!size.x || !size.y) return;
        const bounds = map.getBounds(), visible = this.visibleBounds(), center = map.getCenter();
        this.svg.setAttribute('width', size.x); this.svg.setAttribute('height', size.y);
        L.DomUtil.setPosition(this.svg, map.containerPointToLayerPoint([0, 0]));
        const lines = element('g', { class: 'reference-grid-lines' }), labels = element('g', { class: 'reference-grid-labels' });
        this.svg.replaceChildren(lines, labels); delete this.svg.dataset.stepMeters; delete this.svg.dataset.epsg;
        const caption = text => { const node = element('text', { x: visible.right - 10, y: visible.height - 16, 'text-anchor': 'end', class: 'reference-grid-caption' }); node.textContent = text; labels.append(node); };
        if (center.lat < -80 || center.lat >= 84) { caption(this.options.unavailable); return; }
        if (bounds.getEast() - bounds.getWest() > 12 || bounds.getNorth() - bounds.getSouth() > 12) { caption(this.options.zoomIn); return; }
        const projection = utmProjection(center.lat, center.lng);
        this.svg.dataset.epsg = projection.code;
        const project = point => projection.forward([point.lng, point.lat]);
        const mid = map.latLngToContainerPoint(center), origin = project(center), nearby = project(map.containerPointToLatLng([mid.x + 120, mid.y]));
        const step = metricGridStep(Math.hypot(nearby[0] - origin[0], nearby[1] - origin[1]));
        this.svg.dataset.stepMeters = step;
        const border = [];
        for (let i = 0; i <= 8; i++) for (const point of [[size.x * i / 8, 0], [size.x * i / 8, size.y], [0, size.y * i / 8], [size.x, size.y * i / 8]])
            border.push(project(map.containerPointToLatLng(point)));
        const finite = border.filter(p => p.every(Number.isFinite)); if (!finite.length) return;
        const minE = Math.min(...finite.map(p => p[0])) - step, maxE = Math.max(...finite.map(p => p[0])) + step;
        const minN = Math.min(...finite.map(p => p[1])) - step, maxN = Math.max(...finite.map(p => p[1])) + step;
        const toScreen = coordinate => {
            let [lon, lat] = projection.inverse(coordinate);
            if (!Number.isFinite(lon) || !Number.isFinite(lat) || lat < -80 || lat >= 84 || (projection.south ? lat > 0 : lat < 0)) return null;
            lon += 360 * Math.round((center.lng - lon) / 360);
            return map.latLngToContainerPoint([lat, lon]);
        };
        const intersection = (points, axis, target) => {
            for (let i = 1; i < points.length; i++) {
                const a = points[i - 1], b = points[i]; if (!a || !b || a[axis] === b[axis]) continue;
                const t = (target - a[axis]) / (b[axis] - a[axis]);
                if (t >= 0 && t <= 1) return { x: a.x + t * (b.x - a.x), y: a.y + t * (b.y - a.y) };
            }
            return null;
        };
        const numberFormatter = new Intl.NumberFormat(this.options.locale, { useGrouping: false, maximumFractionDigits: Math.max(0, Math.ceil(Math.log10(1000 / step))) });
        const drawLine = (value, easting) => {
            const points = [];
            for (let i = 0; i <= 16; i++) points.push(toScreen(easting ? [value, minN + (maxN - minN) * i / 16] : [minE + (maxE - minE) * i / 16, value]));
            let d = '', connected = false;
            for (const point of points) { if (!point) { connected = false; continue; } d += `${connected ? 'L' : 'M'}${point.x} ${point.y} `; connected = true; }
            if (!d) return;
            lines.append(element('path', { d, 'data-axis': easting ? 'easting' : 'northing', 'data-value': value, class: value % 1000 ? 'grid-minor' : 'grid-major', 'vector-effect': 'non-scaling-stroke' }));
            const hit = intersection(points, easting ? 'y' : 'x', easting ? 22 : visible.left + 10);
            if (!hit || (easting ? hit.x < visible.left + 48 || hit.x > visible.right - 48 : hit.y < 40 || hit.y > visible.height - 36)) return;
            const label = element('text', { x: hit.x, y: easting ? 18 : hit.y - 5, 'text-anchor': easting ? 'middle' : 'start', 'data-axis': easting ? 'easting' : 'northing', 'data-value': value });
            const number = numberFormatter.format(value / 1000);
            const prefixLength = Math.max(0, String(Math.trunc(Math.abs(value) / 1000)).length - 2);
            if (prefixLength && value >= 0) { const prefix = element('tspan', { class: 'reference-grid-prefix' }); prefix.textContent = number.slice(0, prefixLength); label.append(prefix, document.createTextNode(number.slice(prefixLength))); }
            else label.textContent = number;
            label.append(document.createTextNode(' ' + this.options.cardinals[easting ? 1 : 0])); labels.append(label);
        };
        const each = (min, max, callback) => { const first = Math.ceil(min / step), last = Math.min(Math.floor(max / step), first + 63); for (let i = first; i <= last; i++) callback(i * step); };
        each(minE, maxE, value => drawLine(value, true)); each(minN, maxN, value => drawLine(value, false));
        const spacing = new Intl.NumberFormat(this.options.locale).format(step >= 1000 ? step / 1000 : step) + (step >= 1000 ? ' km' : ' m');
        caption(`WGS 84 / UTM ${projection.zone}${projection.south ? 'S' : 'N'} · ${this.options.stepLabel} ${spacing} · ${this.options.cardinals[1]}/${this.options.cardinals[0]}: km`);
    }
    dispose() { cancelAnimationFrame(this.frame); this.map.off('move zoom resize', this.schedule); this.svg.remove(); }
}
