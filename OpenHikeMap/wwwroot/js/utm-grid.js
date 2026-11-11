import '../lib/proj4/proj4.js';

export function utmProjection(latitude, longitude) {
    const wrapped = ((longitude + 180) % 360 + 360) % 360 - 180;
    const lon = wrapped === -180 && longitude > 0 ? 180 : wrapped;
    let zone = Math.max(1, Math.min(60, Math.floor((lon + 180) / 6) + 1));
    if (latitude >= 56 && latitude < 64 && lon >= 3 && lon < 12) zone = 32;
    if (latitude >= 72 && latitude < 84 && lon >= 0 && lon < 42) zone = lon < 9 ? 31 : lon < 21 ? 33 : lon < 33 ? 35 : 37;
    const south = latitude < 0, code = `EPSG:${(south ? 32700 : 32600) + zone}`;
    const transform = globalThis.proj4('EPSG:4326', `+proj=utm +zone=${zone}${south ? ' +south' : ''} +datum=WGS84 +units=m +no_defs`);
    return { zone, south, code, forward: coordinate => transform.forward(coordinate), inverse: coordinate => transform.inverse(coordinate) };
}

export function metricGridStep(target) {
    return [10, 20, 50, 100, 200, 500, 1000, 2000, 5000, 10000, 20000, 50000, 100000, 200000, 500000].find(value => value >= target) || 500000;
}
