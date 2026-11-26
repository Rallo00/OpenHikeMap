import { ReferenceGrid } from './reference-grid.js';
import { WebcamOverlay } from './webcams.js';

const empty = () => ({ type: 'FeatureCollection', features: [] });
const feature = (geometry, properties = {}) => ({ type: 'Feature', geometry, properties });
const osm = '© <a href="https://www.openstreetmap.org/copyright" target="_blank" rel="noopener">OpenStreetMap</a> contributors';
const terrainCredit = '<a href="https://mapterhorn.com/attribution" target="_blank" rel="noopener">Terrain: Mapterhorn · source attributions</a>';
const sources = {
    OpenStreetMap: ['/tiles/osm/{z}/{x}/{y}.png', 19, osm],
    OpenTopoMap: ['https://a.tile.opentopomap.org/{z}/{x}/{y}.png', 17, `${osm} | SRTM | © OpenTopoMap (CC-BY-SA)`],
    CyclOSM: ['https://a.tile-cyclosm.openstreetmap.fr/cyclosm/{z}/{x}/{y}.png', 19, `${osm} | CyclOSM`],
    Humanitarian: ['https://a.tile.openstreetmap.fr/hot/{z}/{x}/{y}.png', 19, `${osm} | Humanitarian OpenStreetMap Team`],
    'OSM DE': ['https://tile.openstreetmap.de/{z}/{x}/{y}.png', 19, osm],
    satellite: ['https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}', 19, 'Tiles © Esri — Esri, Maxar, Earthstar Geographics, GIS User Community'],
    trails: ['https://tile.waymarkedtrails.org/hiking/{z}/{x}/{y}.png', 18, `${osm} | Waymarked Trails`],
    contours: ['https://tile.osm.ch/contours/{z}/{x}/{y}.png', 18, 'Contours © <a href="https://www.osm.ch/">OSM Switzerland</a>']
};

// Load the renderer and its worker only on entry into 3D; the 2D startup has no WebGL dependency.
export async function createTerrainMap(container, camera, callbacks, visibleBounds, labels) {
    if (!document.querySelector('link[data-maplibre]')) {
        const css = document.createElement('link'); css.rel = 'stylesheet'; css.href = '/lib/maplibre/maplibre-gl.css'; css.dataset.maplibre = ''; document.head.append(css);
    }
    const gl = await import('../lib/maplibre/maplibre-gl.mjs');
    gl.setWorkerUrl(new URL('../lib/maplibre/maplibre-gl-worker.mjs', import.meta.url).href);
    gl.setWorkerCount(2);
    const view = new TerrainMap(gl, container, camera, callbacks, visibleBounds, labels);
    try { await view.ready; return view; } catch (error) { view.dispose(); throw error; }
}

class TerrainMap {
    constructor(gl, container, camera, callbacks, visibleBounds, labels) {
        this.gl = gl; this.container = container; this.callbacks = callbacks; this.visibleBounds = visibleBounds;
        this.pins = new Map(); this.labels = labels; this.panels = new Map(); this.path = [];
        this.map = new gl.Map({ container, center: [camera.lng, camera.lat], zoom: camera.zoom, pitch: 70, maxPitch: 85, maxZoom: 19,
            attributionControl: false, maxTileCacheSize: 160, style: { version: 8,
                sources: { terrain: { type: 'raster-dem', url: 'https://tiles.mapterhorn.com/tilejson.json' },
                    hillshade: { type: 'raster-dem', url: 'https://tiles.mapterhorn.com/tilejson.json' },
                    base: this.raster(camera.style) },
                layers: [{ id: 'base', type: 'raster', source: 'base' }, { id: 'hillshade', type: 'hillshade', source: 'hillshade', paint: { 'hillshade-shadow-color': '#473B24' } }],
                terrain: { source: 'terrain', exaggeration: 1 }, sky: {} },
            locale: { 'NavigationControl.ZoomIn': labels.zoomIn, 'NavigationControl.ZoomOut': labels.zoomOut, 'NavigationControl.ResetBearing': labels.resetBearing,
                'TerrainControl.Enable': labels.enableTerrain, 'TerrainControl.Disable': labels.disableTerrain, 'AttributionControl.ToggleAttribution': labels.attribution } });
        this.map.addControl(new gl.AttributionControl({ compact: true, customAttribution: terrainCredit }), 'bottom-right');
        this.map.addControl(new gl.NavigationControl({ visualizePitch: true, showZoom: true, showCompass: true }), 'bottom-right');
        this.map.addControl(new gl.TerrainControl({ source: 'terrain', exaggeration: 1 }), 'bottom-right');
        this.baseName = camera.style;
        this.map.on('click', e => {
            const warning = this.map.getLayer('warnings') ? this.map.queryRenderedFeatures(e.point, { layers: ['warnings'] })[0] : null;
            if (warning) callbacks.alert(this.state.alerts[Number(warning.properties.index)], e.lngLat); else callbacks.click(e.lngLat);
        });
        this.map.on('contextmenu', e => { e.preventDefault(); callbacks.context(e.lngLat); });
        this.map.on('mousemove', e => callbacks.mousemove(e.lngLat));
        this.map.on('move', () => { this.updateScale(); callbacks.move(); });
        this.map.on('error', e => { if (e.sourceId === 'terrain') this.setNotice(this.labels.dataUnavailable); });
        this.map.on('webglcontextlost', () => this.setNotice(this.labels.unavailable));
        this.map.on('terrain', () => this.updateControlLabels());
        this.ready = new Promise((resolve, reject) => {
            const timeout = setTimeout(() => reject(new Error('3D map load timeout')), 20000);
            this.map.once('load', () => { clearTimeout(timeout); this.loadTimer = null; resolve(); }); this.loadTimer = timeout;
        });
        this.adapter = this.projectionAdapter();
    }
    raster(name) { const [url, maxzoom, attribution] = sources[name] || sources.OpenStreetMap; return { type: 'raster', tiles: [url.startsWith('/') ? location.origin + url : url], tileSize: 256, maxzoom, attribution }; }
    projectionAdapter() {
        const view = this, map = this.map;
        return {
            getContainer: () => view.container, getSize: () => ({ x: view.container.clientWidth, y: view.container.clientHeight }),
            getCenter: () => map.getCenter(), getBounds: () => map.getBounds(),
            containerPointToLatLng: p => map.unproject(p), containerPointToLayerPoint: p => L.point(p[0], p[1]),
            latLngToContainerPoint: p => map.project(Array.isArray(p) ? [p[1], p[0]] : p),
            getPane: name => view.panels.get(name),
            createPane(name) { const node = document.createElement('div'); node.className = 'terrain-pane'; view.container.append(node); view.panels.set(name, node); return node; },
            on(names, callback) { for (const name of names.split(' ')) map.on(name, callback); },
            off(names, callback) { for (const name of names.split(' ')) map.off(name, callback); },
            attributionControl: { addAttribution() {}, removeAttribution() {} }
        };
    }
    render(state) {
        if (this.disposed) return;
        this.state = state; this.labels = state.labels.terrain || this.labels;
        this.updateControlLabels();
        const { settings, places, stops, path, draft, selectedId, labels, alerts, weatherPoint } = state;
        const map = this.map;
        if (this.baseName !== settings.mapStyle) {
            if (map.getLayer('base')) map.removeLayer('base'); if (map.getSource('base')) map.removeSource('base');
            map.addSource('base', this.raster(settings.mapStyle)); map.addLayer({ id: 'base', type: 'raster', source: 'base' }, 'hillshade'); this.baseName = settings.mapStyle;
        }
        for (const [id, enabled] of [['satellite', settings.showSatelliteOverlay], ['trails', settings.showHikingTrails], ['contours', settings.showContourLines]]) {
            if (enabled && !map.getSource(id)) { map.addSource(id, this.raster(id)); map.addLayer({ id, type: 'raster', source: id, paint: { 'raster-opacity': id === 'contours' ? .75 : 1 } }); }
            if (map.getLayer(id)) map.setLayoutProperty(id, 'visibility', enabled ? 'visible' : 'none');
        }
        // Keep route and warnings above every raster overlay, including newly enabled ones.
        if (path) this.path = path;
        this.setGeoJSON('route', this.path.length > 1 ? feature({ type: 'LineString', coordinates: this.path.map(p => [p.longitude, p.latitude]) }) : empty(),
            { type: 'line', paint: { 'line-color': '#c36837', 'line-width': 4, 'line-dasharray': draft ? [2, 2] : [1, 0] }, layout: { 'line-join': 'round', 'line-cap': 'round' } });
        const warningFeatures = settings.showEumetnetAlerts ? (alerts || []).map((area, index) => ({ area, index })).filter(({area}) => !area.expires || Date.parse(area.expires) > Date.now()).map(({area, index}) => feature({ type: 'MultiPolygon', coordinates: area.polygons.map(polygon => polygon.map(ring => {
            const points = ring.map(p => [p.longitude, p.latitude]); if (points.length && (points[0][0] !== points.at(-1)[0] || points[0][1] !== points.at(-1)[1])) points.push(points[0]); return points;
        })) }, { index, color: area.severity >= 4 ? '#C62828' : area.severity >= 3 ? '#EF6C00' : '#C49A00' })) : [];
        this.setGeoJSON('warnings', { type: 'FeatureCollection', features: warningFeatures }, { type: 'fill', paint: { 'fill-color': ['get', 'color'], 'fill-opacity': .12 } });
        if (!map.getLayer('warning-lines')) map.addLayer({ id: 'warning-lines', type: 'line', source: 'warnings', paint: { 'line-color': ['get', 'color'], 'line-width': 2 } });
        for (const id of ['warnings', 'warning-lines', 'route', 'ruler']) if (map.getLayer(id)) map.moveLayer(id);
        const wanted = new Map();
        if (settings.showSavedPoints) for (const place of places) wanted.set(`pin:${place.id}`, { point: place, html: this.callbacks.placeHtml(place, selectedId), title: place.name,
            click: point => this.callbacks.place(place, point), context: point => this.callbacks.place(place, point) });
        let waypoint = 0;
        stops.forEach((stop, index) => { const badge = document.createElement('span'); badge.className = 'route-marker'; badge.textContent = stop.pointType === 'Start' ? 'A' : stop.pointType === 'End' ? 'B' : ++waypoint;
            wanted.set(`stop:${index}`, { point: stop, html: badge, title: stop.name, context: point => this.callbacks.stop(stop, index, point), badge: true }); });
        if (weatherPoint) wanted.set('weather', { point: weatherPoint, html: this.callbacks.weatherHtml, title: labels.weatherReference, draggable: true,
            drag: point => this.callbacks.weather(point) });
        for (const [id, entry] of this.pins) if (!wanted.has(id)) { entry.marker.remove(); this.pins.delete(id); }
        for (const [id, options] of wanted) {
            const signature = JSON.stringify([options.point.latitude, options.point.longitude, options.html instanceof Node ? options.html.outerHTML : options.html, options.title]);
            if (this.pins.get(id)?.signature === signature) continue;
            this.pins.get(id)?.marker.remove(); const marker = this.makeMarker(options); this.pins.set(id, { marker, signature });
        }
        if (settings.showWebcams || this.webcams) { this.webcams ??= new WebcamOverlay(this.adapter, this.visibleBounds, this.webcamGraphics()); this.webcams.setOptions(Boolean(settings.showWebcams), labels.webcams); }
        if (settings.showCoordinateGrid) { this.grid ??= new ReferenceGrid(this.adapter, this.visibleBounds); this.grid.setOptions({ cardinals: labels.gridCardinals, locale: settings.languageCode, stepLabel: labels.gridStep, zoomIn: labels.gridZoomIn, unavailable: labels.gridUnavailable }); }
        else { this.grid?.dispose(); this.grid = null; }
        this.map.getCanvas().style.cursor = this.mode && this.mode !== 'explore' ? 'crosshair' : '';
        this.updateScale(); this.resize();
    }
    setGeoJSON(id, data, layer) {
        if (this.map.getSource(id)) this.map.getSource(id).setData(data);
        else { this.map.addSource(id, { type: 'geojson', data }); this.map.addLayer({ id, source: id, ...layer }); }
        if (layer.paint) for (const [key, value] of Object.entries(layer.paint)) this.map.setPaintProperty(id, key, value);
    }
    makeMarker(options, add = true) {
        const element = document.createElement('div'); element.className = `terrain-marker${options.badge ? ' terrain-stop' : ''}`; element.title = options.title || '';
        if (options.html instanceof Node) element.append(options.html); else element.innerHTML = options.html;
        const marker = new this.gl.Marker({ element, anchor: options.badge ? 'center' : 'bottom', draggable: Boolean(options.draggable), pitchAlignment: 'viewport', rotationAlignment: 'viewport' })
            .setLngLat([options.point.longitude, options.point.latitude]);
        for (const [type, callback] of [['click', options.click], ['contextmenu', options.context]]) if (callback) element.addEventListener(type, e => { e.preventDefault(); e.stopPropagation(); callback(marker.getLngLat()); });
        if (options.drag) marker.on('dragend', () => options.drag(marker.getLngLat()));
        element.addEventListener('keydown', e => { if ((e.key === 'Enter' || e.key === ' ') && options.click) { e.preventDefault(); options.click(marker.getLngLat()); } });
        if (options.click || options.context) { element.tabIndex = 0; element.setAttribute('role', 'button'); element.setAttribute('aria-label', options.title || ''); }
        if (add) marker.addTo(this.map); return marker;
    }
    webcamGraphics() {
        const view = this;
        return {
            layerGroup() { return { entries: new Set(), addTo() { return this; }, removeLayer(entry) { entry.remove(); this.entries.delete(entry); }, clearLayers() { for (const entry of this.entries) entry.remove(); this.entries.clear(); }, remove() { this.clearLayers(); } }; },
            marker(camera, svg) {
                const marker = view.makeMarker({ point: camera, title: camera.name, html: `<div class="map-pin webcam-pin">${svg}</div>`, click: () => window.open(camera.url, '_blank', 'noopener,noreferrer') }, false);
                return { addTo(group) { group.entries.add(this); marker.addTo(view.map); return this; }, remove() { marker.remove(); } };
            }
        };
    }
    showPopup(point, content) { this.closePopup(); this.popup = new this.gl.Popup({ closeButton: false, maxWidth: '390px', className: 'terrain-context' }).setLngLat(point).setDOMContent(content).addTo(this.map); }
    closePopup() { this.popup?.remove(); this.popup = null; }
    center(lat, lon, zoom) { this.map.flyTo({ center: [lon, lat], zoom: Math.max(0, zoom - 1), duration: 3000 }); }
    fit(path) {
        if (!path.length) return; const bounds = new this.gl.LngLatBounds(); path.forEach(p => bounds.extend([p.longitude, p.latitude])); const visible = this.visibleBounds();
        this.map.fitBounds(bounds, { padding: { left: visible.left + 25, right: this.container.clientWidth - visible.right + 25, top: 25, bottom: visible.bottom + 25 }, maxZoom: 15, duration: 1200 });
    }
    setMode(mode) { this.mode = mode; this.map.getCanvas().style.cursor = mode === 'explore' ? '' : 'crosshair'; this.setRuler(); }
    setRuler(points = [], text = '') {
        this.setGeoJSON('ruler', points.length > 1 ? feature({ type: 'LineString', coordinates: points.map(p => [p.lng, p.lat]) }) : empty(), { type: 'line', paint: { 'line-color': '#1f6feb', 'line-width': 3, 'line-dasharray': [2, 2] } });
        this.rulerLabel?.remove(); this.rulerLabel = null;
        if (text && points.length > 1) { const node = document.createElement('span'); node.className = 'ruler-label terrain-ruler'; node.textContent = text;
            this.rulerLabel = new this.gl.Marker({ element: node, anchor: 'bottom' }).setLngLat([(points[0].lng + points[1].lng) / 2, (points[0].lat + points[1].lat) / 2]).addTo(this.map); }
    }
    updateScale() {
        if (!this.state) return; const visible = this.visibleBounds();
        const right = this.container.querySelector('.maplibregl-ctrl-bottom-right'); if (right) { right.style.bottom = `${visible.bottom + 8}px`; right.style.right = `${this.container.clientWidth - visible.right + 8}px`; }
        if (!this.state.settings.showMapScale) { this.scale?.remove(); this.scale = null; return; }
        if (!this.scale) { this.scale = document.createElement('div'); this.scale.className = 'modern-scale terrain-scale'; this.scale.innerHTML = '<div class="modern-scale-label"></div><div class="modern-scale-pill"></div>'; this.container.append(this.scale); }
        const y = visible.height - 30, a = this.map.unproject([visible.left + 20, y]), b = this.map.unproject([visible.left + 160, y]), max = a.distanceTo(b);
        if (!Number.isFinite(max) || max <= 0) return;
        const exponent = 10 ** Math.floor(Math.log10(max)); let meters = exponent; for (const value of [1, 2, 5]) if (value * exponent <= max) meters = value * exponent;
        this.scale.style.left = `${visible.left + 8}px`; this.scale.style.bottom = `${visible.bottom + 16}px`;
        this.scale.querySelector('.modern-scale-label').textContent = this.callbacks.scaleLabel(meters);
        this.scale.querySelector('.modern-scale-pill').style.width = `${Math.max(36, Math.min(140, meters / max * 140))}px`;
    }
    setNotice(text) { if (!this.notice) { this.notice = document.createElement('div'); this.notice.className = 'terrain-notice'; this.notice.setAttribute('role', 'status'); this.container.append(this.notice); } this.notice.textContent = text; }
    updateControlLabels() {
        for (const [selector, text] of [['.maplibregl-ctrl-zoom-in', this.labels.zoomIn], ['.maplibregl-ctrl-zoom-out', this.labels.zoomOut], ['.maplibregl-ctrl-compass', this.labels.resetBearing],
            ['.maplibregl-ctrl-terrain', this.map.getTerrain() ? this.labels.disableTerrain : this.labels.enableTerrain], ['.maplibregl-ctrl-attrib-button', this.labels.attribution]]) {
            const button = this.container.querySelector(selector); if (button && text) { button.title = text; button.setAttribute('aria-label', text); }
        }
    }
    resize() {
        const visible = this.visibleBounds(), key = JSON.stringify([this.container.clientWidth, this.container.clientHeight, visible.left, visible.right, visible.bottom]);
        if (this.sizeKey === key) return; this.sizeKey = key;
        this.map.resize(); this.map.setPadding({ left: visible.left, right: this.container.clientWidth - visible.right, bottom: visible.bottom, top: 0 });
    }
    dispose() { if (this.disposed) return; this.disposed = true; clearTimeout(this.loadTimer); this.webcams?.dispose(); this.grid?.dispose(); this.closePopup(); this.rulerLabel?.remove(); for (const entry of this.pins.values()) entry.marker.remove(); this.pins.clear(); this.map.remove(); this.container.remove(); }
}
