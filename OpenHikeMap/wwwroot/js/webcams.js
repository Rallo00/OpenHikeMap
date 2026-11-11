const webcamSvg = '<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class="lucide lucide-webcam preview-icon" aria-hidden="true"><circle cx="12" cy="10" r="8"/><circle cx="12" cy="10" r="3"/><path d="M7 22h10"/><path d="M12 22v-4"/></svg>';

// A separate layer: catalogue cameras never become saved places or route points.
export class WebcamOverlay {
    constructor(map, visibleBounds) {
        this.map = map; this.visibleBounds = visibleBounds; this.enabled = false; this.sequence = 0; this.markers = new Map();
        if (!map.getPane('webcams')) { map.createPane('webcams'); map.getPane('webcams').style.zIndex = 580; }
        this.layer = L.layerGroup();
        this.status = L.DomUtil.create('div', 'webcam-status'); this.status.hidden = true; this.status.setAttribute('role', 'status'); this.status.setAttribute('aria-live', 'polite');
        map.getContainer().append(this.status);
        this.moving = () => this.cancel(); this.moved = () => this.schedule();
        map.on('movestart zoomstart', this.moving); map.on('moveend zoomend', this.moved);
    }
    setOptions(enabled, labels) {
        this.labels = labels;
        const changed = this.enabled !== enabled; this.enabled = enabled;
        if (!enabled) {
            this.cancel(); this.layer.remove(); this.layer.clearLayers(); this.markers.clear(); this.showStatus();
            if (this.attribution) { this.map.attributionControl?.removeAttribution('Webcams: <a href="https://opencam.live/" target="_blank" rel="noopener">OpenCam</a>'); this.attribution = false; }
            return;
        }
        if (changed) {
            this.layer.addTo(this.map);
            this.map.attributionControl?.addAttribution('Webcams: <a href="https://opencam.live/" target="_blank" rel="noopener">OpenCam</a>'); this.attribution = true;
            this.schedule();
        } else this.showStatus(this.statusKey);
    }
    cancel() { clearTimeout(this.timer); this.sequence++; this.request?.abort(); this.request = null; }
    schedule() {
        if (!this.enabled) return;
        this.cancel(); this.timer = setTimeout(() => this.load(), 500);
    }
    bounds() {
        const visible = this.visibleBounds(), size = this.map.getSize();
        const nw = this.map.containerPointToLatLng([visible.left, 0]), se = this.map.containerPointToLatLng([visible.right, visible.height]);
        const south = Math.max(-85.051129, se.lat), north = Math.min(85.051129, nw.lat);
        if (visible.right <= visible.left || visible.height <= 0 || size.x <= 0 || south >= north) return [];
        const width = se.lng - nw.lng;
        if (width >= 360) return [[-180, south, 180, north]];
        const west = ((nw.lng + 180) % 360 + 360) % 360 - 180, east = west + width;
        return east <= 180 ? [[west, south, east, north]] : [[west, south, 180, north], [-180, south, east - 360, north]];
    }
    showStatus(key = '') {
        this.statusKey = key; this.status.hidden = !key || !this.enabled; this.status.textContent = this.labels?.[key] || '';
        const visible = this.visibleBounds(); this.status.style.left = `${visible.left + 12}px`; this.status.style.bottom = `${visible.bottom + 55}px`;
        this.status.style.maxWidth = `${Math.max(100, visible.right - visible.left - 24)}px`;
    }
    async load() {
        if (!this.enabled) return;
        const sequence = this.sequence, bounds = this.bounds(); if (!bounds.length) return;
        const request = new AbortController(); this.request = request; this.showStatus('loading');
        try {
            const results = await Promise.all(bounds.map(async box => {
                const query = new URLSearchParams(Object.fromEntries(['west', 'south', 'east', 'north'].map((key, i) => [key, box[i].toFixed(5)])));
                const response = await fetch(`/webcams?${query}`, { signal: request.signal });
                if (!response.ok) throw new Error(`OpenCam ${response.status}`);
                const result = await response.json(); if (!Array.isArray(result.cameras)) throw new Error('Invalid webcam response'); return result;
            }));
            if (!this.enabled || sequence !== this.sequence) return;
            const cameras = new Map(results.flatMap(result => result.cameras).filter(camera => typeof camera.id === 'string' && Number.isFinite(camera.latitude) && Number.isFinite(camera.longitude)).map(camera => [camera.id, camera]));
            for (const [id, marker] of this.markers) if (!cameras.has(id)) { this.layer.removeLayer(marker); this.markers.delete(id); }
            for (const [id, camera] of cameras) {
                const signature = JSON.stringify(camera), previous = this.markers.get(id);
                if (previous?._webcamSignature === signature) continue;
                if (previous) this.layer.removeLayer(previous);
                const icon = L.divIcon({ html: `<div class="map-pin webcam-pin" style="--pin-color:#fff">${webcamSvg}</div>`, className: 'openhikemap-marker', iconSize: [40, 52], iconAnchor: [20, 50] });
                const title = document.createElement('span'); title.textContent = camera.name;
                const marker = L.marker([camera.latitude, camera.longitude], { icon, pane: 'webcams', riseOnHover: true, bubblingMouseEvents: false, title: camera.name, alt: camera.name })
                    .bindTooltip(title, { direction: 'top', offset: [0, -39] }).on('click', event => {
                        L.DomEvent.stopPropagation(event);
                        const url = new URL(camera.url); if (url.protocol === 'https:' && url.hostname === 'opencam.live' && url.pathname === '/globe') window.open(url.href, '_blank', 'noopener,noreferrer');
                    }).addTo(this.layer);
                marker._webcamSignature = signature; this.markers.set(id, marker);
            }
            this.showStatus(results.some(result => result.truncated) ? 'zoomIn' : cameras.size ? '' : 'empty');
        } catch (error) { if (!request.signal.aborted && this.enabled && sequence === this.sequence) this.showStatus('unavailable'); }
        finally { if (this.request === request) this.request = null; }
    }
    dispose() {
        this.setOptions(false, this.labels); this.map.off('movestart zoomstart', this.moving); this.map.off('moveend zoomend', this.moved); this.status.remove();
    }
}
