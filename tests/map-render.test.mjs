import assert from 'node:assert/strict';
import { initialize, render, dispose } from '../OpenHikeMap/wwwroot/js/map.js';

const element = () => ({ style: {}, append() {}, remove() {}, setAttribute() {}, replaceChildren() {} });
const container = element(), panes = {}, active = new Set(), markers = [], polygons = [], calls = [];
globalThis.document = { getElementById: () => container, querySelector: () => null, createElement: element };
globalThis.requestAnimationFrame = callback => callback();
globalThis.ResizeObserver = class { observe() {} disconnect() {} };
const map = {
    setView() { return this; }, on() { return this; }, createPane(name) { panes[name] = element(); }, getPane(name) { return panes[name]; },
    invalidateSize() {}, hasLayer: layer => active.has(layer), removeLayer(layer) { active.delete(layer); }, remove() {}, getContainer: () => container
};
function layer() { return { addTo(target) { active.add(this); return this; }, clearLayers() {}, bindTooltip() { return this; }, bindPopup() { return this; }, setLatLngs() { return this; }, setStyle() { return this; } }; }
globalThis.L = {
    map: () => map, control: { zoom: () => layer() }, tileLayer: () => layer(), layerGroup: () => layer(), divIcon: value => value,
    polyline: () => layer(), polygon(points, options) { const value = layer(); polygons.push({ points, options }); return value; },
    marker(coordinates, options) {
        const value = Object.assign(layer(), { coordinates, options, handlers: {}, on(name, fn) { this.handlers[name] = fn; return this; },
            getLatLng() { return { lat: this.coordinates[0], lng: this.coordinates[1] }; }, setLatLng(value) { this.coordinates = value; return this; } });
        markers.push(value); return value;
    }
};
const settings = { defaultLatitude: 40, defaultLongitude: 15, defaultZoom: 10, mapStyle: 'OpenStreetMap', languageCode: 'en-US', showEumetnetAlerts: true };
await initialize('map', { invokeMethodAsync: (...args) => { calls.push(args); return Promise.resolve(); } }, settings);
const area = { name: 'Italy', event: 'Wind', details: 'Description', sender: 'Official service', issued: new Date().toISOString(), onset: new Date().toISOString(), expires: null, severity: 2,
    polygons: [[[{ latitude: 39, longitude: 14 }, { latitude: 39, longitude: 16 }, { latitude: 41, longitude: 16 }]]] };
render(settings, [], [], [], false, null, {}, [area, { ...area, name: 'Germany' }], { latitude: 40.1, longitude: 15.1 });
assert.equal(polygons.length, 2, 'All supplied warning zones are drawn');
assert.equal(markers.length, 1, 'Completed circuit renders a weather marker');
assert.equal(markers[0].options.pane, 'weatherReference');
assert.ok(panes.weatherReference.style.zIndex > 600, 'Weather marker is above normal route pins');
render(settings, [], [], null, false, null, {}, null, { latitude: 40.2, longitude: 15.2 });
assert.equal(polygons.length, 2, 'Unchanged startup warnings are not redrawn or reloaded');
assert.equal(markers.length, 1, 'The weather marker is reused');
assert.deepEqual(markers[0].coordinates, [40.2, 15.2], 'The weather marker follows its current calculation point');
markers[0].handlers.dragend({ target: markers[0] });
assert.deepEqual(calls.at(-1), ['MapAction', 40.2, 15.2, 'weather', '', -1], 'Dragging the point requests a weather reference change');
render({ ...settings, showEumetnetAlerts: false }, [], [], null, false, null, {}, null, { latitude: 40.2, longitude: 15.2 });
render(settings, [], [], null, false, null, {}, null, { latitude: 40.2, longitude: 15.2 });
assert.equal(polygons.length, 4, 'Re-enabling the overlay uses the cached complete zone set');
dispose();
console.log('PASS map warning cache, complete circuit marker, point movement and overlay reuse');
