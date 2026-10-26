let map, bridge, base, layers, markers, routeMarkers, routeLine, scale, bearing, resizeObserver, civilAlerts;
let mode = 'explore', rulerStart, ruler, popup, weatherMarker, unitPreset = 'Metric';
let leafletLoader, searchEvents, renderedStops = '';
let placesById = new Map(), markersByPlaceId = new Map();
let contextLabels = { addPin: 'Aggiungi pin', deletePin: 'Elimina', edit: 'Modifica elemento', save: 'Salva modifiche', cancel: 'Annulla', start: 'Imposta come punto di partenza', waypoint: 'Imposta come waypoint', end: 'Imposta come punto di arrivo', weather: 'Imposta come punto di calcolo meteo', directWaypoint: 'Imposta come waypoint diretto', deleteRoutePoint: 'Elimina punto traccia', name: 'Nome', description: 'Descrizione', icon: 'Icona', color: 'Colore pin' };
const storageKey = 'openhikemap.web.session.v1';
const routeStorageKey = 'openhikemap.web.route.v1';
const iconFiles = { poi: 'mountain', bivouac: 'house_with_flag', fountain: 'water_tap_with_droplet', food: 'fork_and_knife', paid_hut: 'campsite_with_euro', single_tent: 'campsite', tent_area: 'campsites', parking: 'p_wide', cable_car: 'aerial_tram', hammocks: 'conifer_tree_beside_conifer_tree', waterfall: 'waterfall', water: 'waters', summit_cross: 'latin_cross_on_square' };
const color = value => /^#[\da-f]{6}$/i.test(value || '') ? value : '#D65A5A';
const call = (...args) => bridge?.invokeMethodAsync(...args).catch(() => {});
const placePopup = place => {
    const content = document.createElement('article'); content.className = 'place-popup';
    const title = document.createElement('strong'); title.textContent = place.name || 'Punto salvato'; content.append(title);
    if (place.description) { const description = document.createElement('p'); description.textContent = place.description; content.append(description); }
    const coordinates = document.createElement('small'); coordinates.textContent = `${Number(place.latitude).toFixed(5)}, ${Number(place.longitude).toFixed(5)}`; content.append(coordinates);
    return content;
};

export function readStorageStream(includeLibrary) {
    const route = localStorage.getItem(routeStorageKey);
    const legacy = includeLibrary || !route ? localStorage.getItem(storageKey) : null;
    let json = includeLibrary ? legacy || route : route || legacy;
    if (!json) return new Blob([], { type: 'application/json' });
    if (!includeLibrary && !route) {
        const data = JSON.parse(json), settings = data.Settings || data.settings || {};
        json = JSON.stringify({ RoutePoints: data.RoutePoints || data.routePoints || [], RouteGeometry: data.RouteGeometry || data.routeGeometry || [], RouteKind: data.RouteKind || data.routeKind || 'Bozza diretta', Settings: { WeatherReferenceLatitude: settings.WeatherReferenceLatitude ?? settings.weatherReferenceLatitude, WeatherReferenceLongitude: settings.WeatherReferenceLongitude ?? settings.weatherReferenceLongitude } });
    }
    if (includeLibrary && legacy && route) {
        const data = JSON.parse(legacy), current = JSON.parse(route);
        data.RoutePoints = current.RoutePoints || []; data.RouteGeometry = current.RouteGeometry || []; data.RouteKind = current.RouteKind || 'Bozza diretta';
        data.Settings = { ...(data.Settings || data.settings || {}), ...current.Settings };
        json = JSON.stringify(data);
    }
    return new Blob([json], { type: 'application/json' });
}
export function writeRouteStorage(json) {
    try { if (localStorage.getItem(routeStorageKey) !== json) localStorage.setItem(routeStorageKey, json); return true; } catch { return false; }
}
function loadLeaflet() {
    if (globalThis.L) return Promise.resolve(globalThis.L);
    if (!leafletLoader) leafletLoader = new Promise((resolve, reject) => {
        const script = document.createElement('script');
        const version = new URL(import.meta.url).searchParams.get('v') || '';
        script.src = `/lib/leaflet/leaflet.js?v=${encodeURIComponent(version)}`; script.async = true;
        const fail = message => { clearTimeout(timeout); script.remove(); leafletLoader = null; reject(new Error(message)); };
        const timeout = setTimeout(() => fail('Timeout durante il caricamento di Leaflet.'), 15000);
        script.onload = () => { clearTimeout(timeout); if (globalThis.L) resolve(globalThis.L); else fail('Leaflet è stato caricato senza l’oggetto L.'); };
        script.onerror = () => fail('Impossibile caricare lib/leaflet/leaflet.js.');
        document.head.append(script);
    });
    return leafletLoader;
}
export async function initialize(id, dotnet, settings) {
    const L = await loadLeaflet();
    const container = document.getElementById(id);
    if (!container) throw new Error(`Contenitore mappa '${id}' non trovato.`);
    dispose();
    bridge = dotnet;
    searchEvents = new AbortController();
    const search = document.querySelector('.header-search');
    if (search) {
        const dismiss = () => call('DismissSearch');
        // Keep focus inside the search when its submit button becomes disabled while loading.
        search.addEventListener('submit', () => search.querySelector('input')?.focus(), { signal: searchEvents.signal, capture: true });
        document.addEventListener('pointerdown', event => { if (!search.contains(event.target)) dismiss(); }, { signal: searchEvents.signal, capture: true });
        search.addEventListener('focusout', event => { if (!search.contains(event.relatedTarget)) dismiss(); }, { signal: searchEvents.signal });
        search.addEventListener('keydown', event => { if (event.key === 'Escape') { event.preventDefault(); dismiss(); } }, { signal: searchEvents.signal });
    }
    // Hot Reload can preserve a Leaflet id on the DOM node even after the old module was unloaded.
    if (container._leaflet_id) { delete container._leaflet_id; container.replaceChildren(); }
    map = L.map(container, { zoomControl: false, worldCopyJump: true, zoomSnap: .5, zoomDelta: .5, zoomAnimation: true, fadeAnimation: true, markerZoomAnimation: true, wheelPxPerZoomLevel: 90 }).setView([settings.defaultLatitude, settings.defaultLongitude], settings.defaultZoom);
    L.control.zoom({ position: 'bottomright' }).addTo(map);
    const osm = '© <a href="https://www.openstreetmap.org/copyright" target="_blank" rel="noopener">OpenStreetMap</a> contributors';
    const tiles = (url, options = {}) => L.tileLayer(url, { attribution: osm, maxZoom: 19, keepBuffer: 3, updateWhenZooming: false, ...options });
    layers = {
        OpenStreetMap: tiles('/tiles/osm/{z}/{x}/{y}.png'),
        OpenTopoMap: tiles('https://{s}.tile.opentopomap.org/{z}/{x}/{y}.png', { maxNativeZoom: 17, attribution: `${osm} | SRTM | © OpenTopoMap (CC-BY-SA)` }),
        CyclOSM: tiles('https://{s}.tile-cyclosm.openstreetmap.fr/cyclosm/{z}/{x}/{y}.png', { attribution: `${osm} | CyclOSM` }),
        Humanitarian: tiles('https://{s}.tile.openstreetmap.fr/hot/{z}/{x}/{y}.png', { attribution: `${osm} | Humanitarian OpenStreetMap Team` }),
        'OSM DE': tiles('https://tile.openstreetmap.de/{z}/{x}/{y}.png'),
        satellite: tiles('https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}', { attribution: 'Tiles © Esri — Source: Esri, Maxar, Earthstar Geographics, and the GIS User Community' }),
        trails: tiles('https://tile.waymarkedtrails.org/hiking/{z}/{x}/{y}.png', { maxNativeZoom: 18, attribution: `${osm} | Waymarked Trails` }),
        contours: tiles('https://tile.osm.ch/contours/{z}/{x}/{y}.png', { maxNativeZoom: 18, opacity: .75, attribution: 'Contours © <a href="https://www.osm.ch/">OSM Switzerland</a>' })
    };
    base = layers[settings.mapStyle] || layers.OpenStreetMap;
    base.addTo(map);
    markers = L.layerGroup().addTo(map);
    routeMarkers = L.layerGroup().addTo(map);
    map.createPane('civilAlerts'); map.getPane('civilAlerts').style.zIndex = 410;
    civilAlerts = L.layerGroup().addTo(map);
    map.on('click', event => {
        if (mode === 'ruler') { measure(event.latlng); return; }
        if (mode !== 'explore') call('MapClick', event.latlng.lat, event.latlng.lng, mode);
    });
    map.on('contextmenu', event => openMapContext(event.latlng));
    map.on('move zoom', () => { drawBearing(); updateModernScale(); });
    map.on('mousemove', event => { if (mode === 'ruler' && rulerStart) renderRuler(event.latlng); });
    resizeObserver = new ResizeObserver(() => { map?.invalidateSize(); drawBearing(); updateModernScale(); });
    resizeObserver.observe(container);
    const library = document.querySelector('.library'); if (library) resizeObserver.observe(library);
    const profile = document.querySelector('.elevation-panel'); if (profile) resizeObserver.observe(profile);
    requestAnimationFrame(() => map?.invalidateSize());
    setTimeout(() => map?.invalidateSize(), 250);
}
const pinAddIcon = '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M12 22v-6M8 3h8a2 2 0 0 1 0 4 1 1 0 0 0-1 1v2.76a2 2 0 0 0 1.11 1.79l1.78.9A2 2 0 0 1 19 15.24V16a1 1 0 0 1-1 1H6a1 1 0 0 1-1-1v-.76a2 2 0 0 1 1.11-1.79l1.78-.9A2 2 0 0 0 9 10.76V8a1 1 0 0 0-1-1 2 2 0 0 1 0-4Z"/><path d="M16 18h6M19 15v6"/></svg>';
const deleteIcon = '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M3 6h18M8 6V4h8v2M19 6l-1 15H6L5 6M10 11v5M14 11v5"/></svg>';
const editIcon = '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M12 20h9"/><path d="M16.5 3.5a2.12 2.12 0 0 1 3 3L7 19l-4 1 1-4Z"/></svg>';
function contextIconButton(label, icon, click) {
    const button = document.createElement('button'); button.type = 'button'; button.className = 'context-icon-button'; button.title = button.setAttribute('aria-label', label) || label; button.innerHTML = icon; button.onclick = click; return button;
}
function contextAction(menu, label, action, latlng, name, routeIndex) {
    const button = document.createElement('button'); button.type = 'button'; button.className = 'context-action'; button.textContent = label;
    button.onclick = () => { map.closePopup(); call('MapAction', latlng.lat, latlng.lng, action, name, routeIndex); }; menu.append(button);
}
function openPlaceEditor(latlng, place) {
    const menu = document.createElement('form'); menu.className = 'map-context-menu place-editor-popup';
    const title = document.createElement('strong'); title.textContent = contextLabels.edit; menu.append(title);
    const field = (labelText, element) => { const label = document.createElement('label'); label.textContent = labelText; label.append(element); menu.append(label); return element; };
    const name = field(contextLabels.name, document.createElement('input')); name.value = place.name || '';
    const description = field(contextLabels.description, document.createElement('textarea')); description.value = place.description || '';
    const icon = field(contextLabels.icon, document.createElement('select')); icon.add(new Option(contextLabels.inherit, ''));
    Object.keys(iconFiles).forEach(key => icon.add(new Option(contextLabels.icons?.[key] || key.replaceAll('_', ' '), key)));
    icon.value = place.iconKey || '';
    const pinColor = field(contextLabels.color, document.createElement('select')); pinColor.add(new Option(contextLabels.inherit, ''));
    Object.entries(contextLabels.colors || {}).forEach(([key, label]) => pinColor.add(new Option(label, key)));
    if (place.pinColorHex && !Array.from(pinColor.options).some(option => option.value.toLowerCase() === place.pinColorHex.toLowerCase())) pinColor.add(new Option(contextLabels.currentColor, place.pinColorHex));
    pinColor.value = Array.from(pinColor.options).find(option => option.value.toLowerCase() === (place.pinColorHex || '').toLowerCase())?.value || '';
    const actions = document.createElement('div'); actions.className = 'context-editor-actions';
    const cancel = document.createElement('button'); cancel.type = 'button'; cancel.textContent = contextLabels.cancel; cancel.onclick = () => map.closePopup(); actions.append(cancel);
    const save = document.createElement('button'); save.type = 'submit'; save.className = 'primary'; save.textContent = contextLabels.save; actions.append(save); menu.append(actions);
    menu.onsubmit = event => { event.preventDefault(); map.closePopup(); call('EditPlace', place.id, name.value, description.value, icon.value, pinColor.value); };
    popup = L.popup({ closeButton: false, autoPan: true, className: 'openhikemap-context' }).setLatLng(latlng).setContent(menu).openOn(map);
}
function openMapContext(latlng, name = '', routeIndex = -1, place = null) {
    const menu = document.createElement('div'); menu.className = 'map-context-menu';
    const heading = document.createElement('div'); heading.className = 'context-menu-heading';
    if (place) {
        heading.append(contextIconButton(contextLabels.deletePin, deleteIcon, () => { map.closePopup(); call('DeletePlaceFromMap', place.id); }));
        heading.append(contextIconButton(contextLabels.edit, editIcon, () => openPlaceEditor(latlng, place)));
    } else heading.append(contextIconButton(contextLabels.addPin, pinAddIcon, () => { map.closePopup(); call('MapAction', latlng.lat, latlng.lng, 'pin', name, routeIndex); }));
    menu.append(heading);
    contextAction(menu, contextLabels.start, 'start', latlng, name, routeIndex);
    contextAction(menu, contextLabels.waypoint, 'waypoint', latlng, name, routeIndex);
    contextAction(menu, contextLabels.end, 'end', latlng, name, routeIndex);
    const separator = document.createElement('hr'); menu.append(separator);
    contextAction(menu, contextLabels.weather, 'weather', latlng, name, routeIndex);
    contextAction(menu, contextLabels.directWaypoint, 'directWaypoint', latlng, name, routeIndex);
    if (routeIndex >= 0) { const routeSeparator = document.createElement('hr'); menu.append(routeSeparator); contextAction(menu, contextLabels.deleteRoutePoint, 'deleteRoutePoint', latlng, name, routeIndex); }
    popup = L.popup({ closeButton: false, autoPan: false, className: 'openhikemap-context' }).setLatLng(latlng).setContent(menu).openOn(map);
}
export function setMode(value) {
    if (!map) return;
    mode = value; rulerStart = null;
    if (ruler) { map.removeLayer(ruler); ruler = null; }
    map.getContainer().style.cursor = value === 'explore' ? '' : 'crosshair';
}
export function render(settings, places, stops, path, draft, selectedId, labels, alertAreas = [], weatherPoint = null) {
    if (!map || !layers) return;
    contextLabels = { ...contextLabels, ...labels };
    const next = layers[settings.mapStyle] || layers.OpenStreetMap;
    if (base !== next) { if (base) map.removeLayer(base); base = next; base.addTo(map); }
    for (const [name, enabled] of [['satellite', settings.showSatelliteOverlay], ['trails', settings.showHikingTrails], ['contours', settings.showContourLines]]) {
        if (enabled && !map.hasLayer(layers[name])) layers[name].addTo(map);
        if (!enabled && map.hasLayer(layers[name])) map.removeLayer(layers[name]);
    }
    const nextPlaces = new Map(places.map(place => [place.id, place]));
    for (const [id, marker] of markersByPlaceId) if (!nextPlaces.has(id)) { markers.removeLayer(marker); markersByPlaceId.delete(id); }
    placesById = nextPlaces;
    for (const place of places) {
        const signature = JSON.stringify([place.name, place.description, place.latitude, place.longitude, place.iconKey, place.pinColorHex, place.effectiveIconKey, place.effectivePinColorHex, place.id === selectedId]);
        const previous = markersByPlaceId.get(place.id);
        if (previous?._ohmSignature === signature) continue;
        if (previous) markers.removeLayer(previous);
        const iconKey = iconFiles[place.effectiveIconKey || place.iconKey] || iconFiles.poi;
        const html = `<div class="map-pin ${place.id === selectedId ? 'map-pin-selected' : ''}" style="--pin-color:${color(place.effectivePinColorHex || place.pinColorHex)}"><img src="assets/pinhead/${iconKey}.svg" alt=""></div>`;
        const icon = L.divIcon({ html, className: 'openhikemap-marker', iconSize: [40, 52], iconAnchor: [20, 50], tooltipAnchor: [0, -42] });
        const marker = L.marker([place.latitude, place.longitude], { icon, riseOnHover: true }).bindTooltip(place.name || 'Punto salvato', { direction: 'top', offset: [0, -39] }).on('click', event => { L.DomEvent.stopPropagation(event); call('PinSelected', place.id); openMapContext(event.latlng, place.name || 'Punto salvato', -1, place); }).on('contextmenu', event => { L.DomEvent.stopPropagation(event); call('PinSelected', place.id); openMapContext(event.latlng, place.name || 'Punto salvato', -1, place); }).addTo(markers);
        marker._ohmSignature = signature; markersByPlaceId.set(place.id, marker);
    }
    if (settings.showSavedPoints && !map.hasLayer(markers)) markers.addTo(map);
    if (!settings.showSavedPoints && map.hasLayer(markers)) map.removeLayer(markers);
    const stopSignature = JSON.stringify(stops);
    if (stopSignature !== renderedStops) {
      routeMarkers.clearLayers();
      let waypointNumber = 0;
      stops.forEach((stop, i) => {
        const badge = document.createElement('span'); badge.className = 'route-marker'; badge.textContent = stop.pointType === 'Start' ? 'A' : stop.pointType === 'End' ? 'B' : ++waypointNumber;
        const label = document.createElement('span'); label.textContent = stop.name;
        L.marker([stop.latitude, stop.longitude], { icon: L.divIcon({ html: badge, className: '', iconSize: [26, 26], iconAnchor: [13, 13] }) }).bindTooltip(label).on('contextmenu', event => { L.DomEvent.stopPropagation(event); openMapContext(event.latlng, stop.name || `Tappa ${i + 1}`, i); }).addTo(routeMarkers);
      });
      renderedStops = stopSignature;
    }
    if (path) {
        const points = path.map(p => [p.latitude, p.longitude]), style = { color: '#c36837', weight: 4, dashArray: draft ? '7 7' : null };
        if (routeLine) routeLine.setLatLngs(points).setStyle(style);
        else routeLine = L.polyline(points, style).addTo(map);
    }
    unitPreset = settings.unitPreset || 'Metric';
    setModernScale(Boolean(settings.showMapScale));
    if (!settings.showBearingCircle) { bearing?.remove(); bearing = null; }
    else if (!bearing) { bearing = document.createElement('div'); bearing.className = 'bearing-overlay'; bearing.setAttribute('aria-hidden', 'true'); map.getContainer().append(bearing); }
    drawBearing();
    civilAlerts.clearLayers();
    if (settings.showCivilProtectionAlerts) for (const area of alertAreas) {
        const content = document.createElement('article'); content.className = 'civil-alert-popup';
        const title = document.createElement('strong'); title.textContent = area.name; content.append(title);
        const detail = document.createElement('p'); detail.textContent = area.details; content.append(detail);
        const source = document.createElement('a'); source.href = 'https://mappe.protezionecivile.gov.it/it/mappe-rischi/bollettino-di-criticita/index.html'; source.target = '_blank'; source.rel = 'noopener'; source.textContent = 'SNPC-IT · Protezione Civile'; content.append(source);
        L.polygon(area.polygons.map(rings => rings.map(ring => ring.map(p => [p.latitude, p.longitude]))), {
            pane: 'civilAlerts', color: area.severity >= 3 ? '#C62828' : '#EF6C00', weight: 2, fillOpacity: .12, bubblingMouseEvents: false,
            attribution: 'Confini: <a href="https://github.com/openpolis/geojson-italy">Openpolis</a> · Allerte: Protezione Civile'
        }).bindTooltip(area.name).bindPopup(content, { className: 'openhikemap-context', maxWidth: 380 }).addTo(civilAlerts);
    }
    if (weatherMarker) { map.removeLayer(weatherMarker); weatherMarker = null; }
    if (weatherPoint) {
        const html = '<div class="map-pin weather-reference"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M12 2v2m-7.07.93 1.41 1.41M20 12h2m-2.93-7.07-1.41 1.41M15.947 12.65a4 4 0 0 0-5.925-4.128M13 22H7a5 5 0 1 1 4.9-6H13a3 3 0 0 1 0 6Z"/></svg></div>';
        weatherMarker = L.marker([weatherPoint.latitude, weatherPoint.longitude], { icon: L.divIcon({ html, className: 'openhikemap-marker', iconSize: [40, 52], iconAnchor: [20, 50] }) }).bindTooltip(contextLabels.weatherReference || 'Riferimento meteo').addTo(map);
    }
}
function visibleMapBounds() {
    const bounds = map.getContainer().getBoundingClientRect(), library = document.querySelector('.library')?.getBoundingClientRect(), details = document.querySelector('.details')?.getBoundingClientRect();
    const profile = document.querySelector('.elevation-panel')?.getBoundingClientRect();
    const bottom = profile && profile.top > bounds.top && profile.top < bounds.bottom ? bounds.bottom - profile.top + 8 : 0;
    return { left: bounds.width > 850 && library ? Math.max(0, library.right - bounds.left) : 0, right: bounds.width > 850 && details ? Math.min(bounds.width, details.left - bounds.left) : bounds.width, height: bounds.height - bottom, bottom };
}
function drawBearing() {
    if (!map || !bearing) return;
    const {left, right, height} = visibleMapBounds(), cx = (left + right) / 2, cy = height / 2, radius = Math.max(45, Math.min(right - left, height) * .34);
    let svg = `<circle cx="${cx}" cy="${cy}" r="${radius}"/>`;
    for (let degrees = 0; degrees < 360; degrees += 15) {
        const angle = (degrees - 90) * Math.PI / 180, major = degrees % 30 === 0, inner = radius - (major ? 14 : 8);
        svg += `<path d="M${cx + Math.cos(angle) * inner} ${cy + Math.sin(angle) * inner}L${cx + Math.cos(angle) * radius} ${cy + Math.sin(angle) * radius}"/>`;
        if (major) {
            const label = degrees % 90 === 0 ? (contextLabels.cardinals || ['N','E','S','W'])[degrees / 90] : `${degrees}°`;
            svg += `<text x="${cx + Math.cos(angle) * (radius + 19)}" y="${cy + Math.sin(angle) * (radius + 19)}">${label}</text>`;
        }
    }
    bearing.innerHTML = `<svg width="100%" height="100%">${svg}</svg>`;
}
function niceScaleDistance(maxMeters) {
    const values = [1, 2, 5]; let exponent = 10 ** Math.floor(Math.log10(Math.max(maxMeters, 1)));
    while (exponent >= .0001) { for (let index = values.length - 1; index >= 0; index--) { const candidate = values[index] * exponent; if (candidate <= maxMeters) return candidate; } exponent /= 10; }
    return maxMeters;
}
function formatScaleDistance(meters) {
    if (unitPreset === 'Imperial') { const feet = meters * 3.28084; return feet >= 5280 ? `${(feet / 5280).toFixed(feet / 5280 >= 10 ? 0 : 1)} mi` : `${Math.round(feet)} ft`; }
    return meters >= 1000 ? `${(meters / 1000).toFixed(meters / 1000 >= 10 ? 0 : 1)} km` : `${Math.round(meters)} m`;
}
function updateModernScale() {
    if (!map) return;
    const bottom = visibleMapBounds().bottom;
    for (const control of map.getContainer().querySelectorAll('.leaflet-bottom')) control.style.bottom = `${bottom}px`;
    const hint = document.querySelector('.map-hint'); if (hint) hint.style.bottom = `${bottom + 24}px`;
    if (!scale || !scale._container) return;
    const centerY = map.getSize().y / 2, maxMeters = map.distance(map.containerPointToLatLng([0, centerY]), map.containerPointToLatLng([140, centerY]));
    const meters = niceScaleDistance(maxMeters), width = Math.max(36, Math.min(140, meters / Math.max(maxMeters, 1) * 140));
    scale._container.style.marginLeft = `${visibleMapBounds().left + 8}px`;
    scale._container.querySelector('.modern-scale-label').textContent = formatScaleDistance(meters);
    scale._container.querySelector('.modern-scale-pill').style.width = `${width.toFixed(0)}px`;
}
function setModernScale(enabled) {
    if (!enabled) { if (scale) { scale.remove(); scale = null; } return; }
    if (!scale) {
        scale = globalThis.L.control({ position: 'bottomleft' });
        scale.onAdd = () => { const element = globalThis.L.DomUtil.create('div', 'modern-scale leaflet-control'); element.innerHTML = '<div class="modern-scale-label"></div><div class="modern-scale-pill"></div>'; globalThis.L.DomEvent.disableClickPropagation(element); return element; };
        scale.addTo(map);
    }
    updateModernScale();
}
function azimuth(start, end) {
    const toRadians = value => value * Math.PI / 180, toDegrees = value => value * 180 / Math.PI;
    const lat1 = toRadians(start.lat), lat2 = toRadians(end.lat), deltaLon = toRadians(end.lng - start.lng);
    return (toDegrees(Math.atan2(Math.sin(deltaLon) * Math.cos(lat2), Math.cos(lat1) * Math.sin(lat2) - Math.sin(lat1) * Math.cos(lat2) * Math.cos(deltaLon))) + 360) % 360;
}
function formatRulerDistance(meters) { return unitPreset === 'Imperial' ? (meters * 3.28084 >= 5280 ? `${(meters / 1609.344).toFixed(1)} mi` : `${Math.round(meters * 3.28084)} ft`) : meters >= 1000 ? `${(meters / 1000).toFixed(1)} km` : `${Math.round(meters)} m`; }
function renderRuler(point) {
    if (!map || !rulerStart) return;
    if (ruler) map.removeLayer(ruler);
    ruler = globalThis.L.polyline([rulerStart, point], { color: '#1f6feb', weight: 3, dashArray: '7 6' }).addTo(map);
    ruler.bindTooltip(`${formatRulerDistance(map.distance(rulerStart, point))} | ${Math.round(azimuth(rulerStart, point))}°`, { permanent: true, direction: 'top', className: 'ruler-label' }).openTooltip();
}
function measure(point) {
    if (!rulerStart) {
        if (ruler) map.removeLayer(ruler);
        rulerStart = point;
        ruler = globalThis.L.circleMarker(point, { radius: 5, color: '#1f6feb', fillOpacity: 1 }).addTo(map);
    } else {
        renderRuler(point);
        rulerStart = null;
    }
}
export function center(lat, lon, zoom) { if (map) { map.stop(); map.flyTo([lat, lon], zoom, { duration: 3 }); } }
export function openPlaceContext(id) {
    const place = placesById.get(id), marker = markersByPlaceId.get(id);
    if (!map || !place || !marker) return;
    const latlng = marker.getLatLng(); map.panTo(latlng); openMapContext(latlng, place.name || 'Punto salvato', -1, place);
}
export function getCenter() { const center = map?.getCenter(); return center ? [center.lat, center.lng] : [0, 0]; }
export function fit(path) { if (map && path.length) { const bounds = visibleMapBounds(); map.fitBounds(L.latLngBounds(path.map(p => [p.latitude, p.longitude])), { paddingTopLeft: [bounds.left + 25, 25], paddingBottomRight: [map.getSize().x - bounds.right + 25, bounds.bottom + 25], maxZoom: 15 }); } }
export function download(filename, text, mime) {
    const url = URL.createObjectURL(new Blob([text], { type: mime + ';charset=utf-8' }));
    const link = document.createElement('a'); link.href = url; link.download = filename; document.body.append(link); link.click(); link.remove(); setTimeout(() => URL.revokeObjectURL(url), 1000);
}
export function dispose() {
    renderedStops = ''; placesById.clear(); markersByPlaceId.clear();
    searchEvents?.abort(); searchEvents = null;
    resizeObserver?.disconnect();
    bearing?.remove();
    if (map) map.remove();
    map = bridge = base = layers = markers = routeMarkers = routeLine = scale = bearing = ruler = popup = weatherMarker = resizeObserver = civilAlerts = null;
    mode = 'explore'; rulerStart = null;
}
