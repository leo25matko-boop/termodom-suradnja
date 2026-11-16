import {
    loadMeasurements, loadPumps, loadWeather, parseMeasurements, parsePumps, readingQuality, weatherCondition
} from './m-house-data.js';

const number = new Intl.NumberFormat('hr-HR', { minimumFractionDigits: 1, maximumFractionDigits: 1 });
const demo = document.body.hasAttribute('data-house-demo');

const sensorKeys = {
    boiler: 'house/kotal/temp', water: 'house/dhw/temp', solar: 'house/solar/temp',
    outdoor: 'house/outdoor/temp', upstairs: 'house/polaz_kat/temp', downstairs: 'house/polaz_priz/temp',
    downstairs2: 'house/polaz_priz2/temp', spare: 'house/sensor1/temp',
    top: 'house/puffer/top', middle: 'house/puffer/midd', bottom: 'house/puffer/bottom'
};
const pumps = [
    ['kotao', 'Kotao'], ['prizemlje', 'Prizemlje'], ['kat', '1. kat'],
    ['sanitarna', 'Sanitarna voda'], ['solar', 'Solarni kolektori']
];
const weatherIcons = { sun: '☀', moon: '☾', 'sun-cloud': '⛅', 'moon-cloud': '☁',
    cloud: '☁', fog: '🌫', rain: '🌧', snow: '❄', storm: '⛈', unknown: '◌' };

const markup = String.raw;
function value(id, label) {
    return markup`<div class="td-reading" data-reading="${id}" data-quality="unknown">
        <span class="td-reading-label">${label}</span><strong data-value>—</strong><time hidden></time>
    </div>`;
}
function buffer(litres) {
    return markup`<article class="td-device td-tank">
        <div class="td-device-icon td-tank-icon" aria-hidden="true"><i></i><i></i><i></i></div>
        <h4>Pufer ${litres} L</h4>
        <div class="td-tank-values">${value(litres + '-top', 'Vrh')}${value(litres + '-middle', 'Sredina')}${value(litres + '-bottom', 'Dno')}</div>
    </article>`;
}
function thermostat(id, name, supply) {
    return markup`<article class="td-floor td-floor--${id}" aria-label="${name}">
        <h3>${name}</h3>
        <div class="td-floor-values">${value(id + '-measured', 'Sobna')}${value(id + '-target', 'Zadana')}</div>
        ${value(supply, 'Polaz')}
    </article>`;
}
function pump(id, label) {
    return markup`<li class="td-pump" data-pump="${id}" data-state="unknown" data-quality="unknown">
        <span class="td-pump-picture" aria-hidden="true"><i class="td-pump-body"></i><i class="td-pump-arrow"></i></span>
        <div><strong>${label}</strong><span class="td-pump-state">Nema podatka</span><time hidden></time></div>
    </li>`;
}

const section = document.createElement('section');
section.className = 'td-house';
section.setAttribute('aria-labelledby', 'td-house-title');
section.innerHTML = markup`
    <header class="td-top">
        <div><p class="td-kicker"><i aria-hidden="true"></i> TERMODOM</p>
            <h1 id="td-house-title">Sustav grijanja</h1><p>Presjek kuće i stanje kotlovnice</p>
        </div>
        <nav aria-label="Rasporedi grijanja" ${demo ? 'hidden' : ''}><a href="prizemlje.php">Prizemlje</a><a href="kat.php">1. kat</a>
            <button type="button" data-refresh>Osvježi</button></nav>
    </header>
    <div class="td-stage">
        <div class="td-diagram">
            <div class="td-diagram-toolbar">
                <h2 id="td-diagram-title">Presjek kuće</h2>
                <div class="td-zoom-controls" role="group" aria-label="Povećanje presjeka">
                    <button type="button" data-zoom-out aria-label="Smanji presjek">−</button>
                    <button type="button" data-zoom-reset aria-label="Vrati prikaz cijele kuće">100 %</button>
                    <button type="button" data-zoom-in aria-label="Povećaj presjek">+</button>
                </div>
            </div>
            <div class="td-viewport" tabindex="0" role="region" aria-labelledby="td-diagram-title">
                <div class="td-zoom-surface">
                    <div class="td-building">
                        <div class="td-roof">${value('solar', 'Solarni kolektori')}</div>
                        ${thermostat('kat', '1. kat', 'upstairs')}
                        ${thermostat('prizemlje', 'Prizemlje', 'downstairs')}
                    </div>
                </div>
            </div>
        </div>
        <div class="td-annex">
        <section class="td-weather" aria-label="Vanjski senzor i vrijeme u Ozlju">
            <div class="td-weather-head"><span class="td-weather-icon" data-weather-icon aria-hidden="true">◌</span>
                <div><h3>Ozalj</h3><p data-weather-status>Učitavanje vremena…</p></div></div>
            <div><p data-forecast-temp></p><a href="https://open-meteo.com/">Open-Meteo</a></div>
            ${value('outdoor', 'Vanjski senzor')}
        </section>
        <section class="td-equipment" aria-labelledby="td-equipment-title">
            <header><h2 id="td-equipment-title">Kotlovnica</h2><span class="td-equipment-dot" aria-hidden="true"></span></header>
            <div class="td-devices">
                <article class="td-device"><div class="td-device-icon td-boiler-icon" aria-hidden="true"><i></i></div><h4>Kotao</h4>${value('boiler', 'Temperatura')}</article>
                ${buffer('2000')}${buffer('1000')}
                <article class="td-device"><div class="td-device-icon td-tank-icon td-water-icon" aria-hidden="true"><i></i></div><h4>Sanitarna voda</h4>${value('water', 'Temperatura')}</article>
                <article class="td-device"><div class="td-device-icon td-heat-pump-icon" aria-hidden="true"><i></i></div><h4>Toplinska pumpa</h4>${value('heat-pump', 'Temperatura')}</article>
            </div>
            <h3 class="td-pump-title">Cirkulacijske pumpe</h3>
            <ul class="td-pumps">${pumps.map(([id, name]) => pump(id, name)).join('')}</ul>
        </section>
        </div>
    </div>
    <div class="td-aux">${value('downstairs2', 'Dodatni polaz prizemlja')}${value('spare', 'Senzor 1')}</div>
    <footer class="td-footer"><p data-status role="status">Učitavanje mjerenja…</p>
        <div class="td-legend"><span><i></i> Aktualno</span><span><i></i> Starije od 3 min</span><span>— Nema podatka</span></div></footer>`;

const oldMain = document.querySelector('main');
const target = document.querySelector('[data-house-mount]');
if (target) {
    target.replaceWith(section);
} else if (oldMain) {
    oldMain.before(section);
    document.body.classList.add('td-house-enabled');
}

function enableDiagramZoom() {
    const viewport = section.querySelector('.td-viewport');
    const surface = section.querySelector('.td-zoom-surface');
    const zoomOut = section.querySelector('[data-zoom-out]');
    const zoomIn = section.querySelector('[data-zoom-in]');
    const zoomReset = section.querySelector('[data-zoom-reset]');
    const pointers = new Map();
    let scale = 1;
    let frameWidth = 0;
    let frameHeight = 0;

    function setZoom(value, anchorX = viewport.clientWidth / 2, anchorY = viewport.clientHeight / 2) {
        const x = (viewport.scrollLeft + anchorX) / (frameWidth * scale || 1);
        const y = (viewport.scrollTop + anchorY) / (frameHeight * scale || 1);
        scale = Math.max(1, Math.min(2.5, value));
        viewport.dataset.zoomed = String(scale > 1);
        frameWidth = viewport.clientWidth;
        frameHeight = viewport.clientHeight;
        surface.style.setProperty('--td-frame-width', frameWidth + 'px');
        surface.style.setProperty('--td-frame-height', frameHeight + 'px');
        surface.style.setProperty('--td-zoom', scale);
        surface.style.width = frameWidth * scale + 'px';
        surface.style.height = frameHeight * scale + 'px';
        viewport.scrollLeft = scale === 1 ? 0 : x * frameWidth * scale - anchorX;
        viewport.scrollTop = scale === 1 ? 0 : y * frameHeight * scale - anchorY;
        zoomOut.disabled = scale <= 1;
        zoomIn.disabled = scale >= 2.5;
        setText(zoomReset, Math.round(scale * 100) + ' %');
    }
    const location = event => {
        const bounds = viewport.getBoundingClientRect();
        return { x: event.clientX - bounds.left, y: event.clientY - bounds.top };
    };
    const geometry = () => {
        const [first, second] = pointers.values();
        return second ? { x: (first.x + second.x) / 2, y: (first.y + second.y) / 2,
            span: Math.hypot(first.x - second.x, first.y - second.y) } : first;
    };
    zoomOut.addEventListener('click', () => setZoom(scale - .25));
    zoomIn.addEventListener('click', () => setZoom(scale + .25));
    zoomReset.addEventListener('click', () => setZoom(1));
    viewport.addEventListener('keydown', event => {
        if (!['+', '=', '-', '0'].includes(event.key)) return;
        event.preventDefault();
        setZoom(event.key === '0' ? 1 : scale + (event.key === '-' ? -.25 : .25));
    });
    viewport.addEventListener('wheel', event => {
        if (!event.ctrlKey) return;
        event.preventDefault();
        const { x, y } = location(event);
        setZoom(scale * Math.exp(-event.deltaY * .003), x, y);
    }, { passive: false });
    viewport.addEventListener('pointerdown', event => {
        if (event.button !== 0 || pointers.size >= 2) return;
        const position = location(event);
        pointers.set(event.pointerId, position);
        viewport.setPointerCapture(event.pointerId);
    });
    viewport.addEventListener('pointermove', event => {
        if (!pointers.has(event.pointerId)) return;
        const before = geometry();
        pointers.set(event.pointerId, location(event));
        const after = geometry();
        if (after.span > 0 && before.span > 0) setZoom(scale * after.span / before.span, before.x, before.y);
        if (scale > 1) {
            viewport.scrollLeft += before.x - after.x;
            viewport.scrollTop += before.y - after.y;
        }
    });
    for (const event of ['pointerup', 'pointercancel', 'lostpointercapture']) {
        viewport.addEventListener(event, e => pointers.delete(e.pointerId));
    }
    setZoom(1);
    new ResizeObserver(() => {
        if (viewport.clientWidth !== frameWidth || viewport.clientHeight !== frameHeight) setZoom(scale);
    }).observe(viewport);
}

const readings = new Map([...section.querySelectorAll('[data-reading]')].map(el => [el.dataset.reading, el]));
const pumpViews = new Map([...section.querySelectorAll('[data-pump]')].map(el => [el.dataset.pump, el]));
const states = { measurements: null, pumps: null };
const errors = { measurements: false, pumps: false };
const requests = new Set();
const dateFormat = new Intl.DateTimeFormat('hr-HR', { dateStyle: 'short', timeStyle: 'short', timeZone: 'Europe/Zagreb' });
function setText(node, value) {
    if (node.textContent !== value) node.textContent = value;
}

function renderClock(node, reading, quality) {
    const source = reading?.receivedAt ?? reading?.updatedAt;
    node.hidden = quality !== 'stale' || !source;
    if (node.hidden) return;
    // The data layer normalizes source timestamps. Unqualified controller time is Zagreb wall time.
    const local = String(source).match(/^(\d{4})-(\d{2})-(\d{2})[T ](\d{2}):(\d{2})(?::\d{2})?$/);
    const instant = local ? null : new Date(source);
    setText(node, local ? `${local[3]}.${local[2]}.${local[1]}. ${local[4]}:${local[5]}` :
        Number.isFinite(instant?.getTime()) ? dateFormat.format(instant) : '');
    node.hidden = !node.textContent;
    if (!node.hidden) node.dateTime = String(source).replace(' ', 'T');
}
function renderValue(key, reading, field = 'temperature') {
    const el = readings.get(key);
    if (!el) return;
    const value = reading?.[field];
    const quality = Number.isFinite(value) ? qualityState(reading) : 'unknown';
    el.dataset.quality = quality;
    setText(el.querySelector('[data-value]'), Number.isFinite(value) ? `${number.format(value)} °C` : '—');
    renderClock(el.querySelector('time'), reading, quality);
}
function qualityState(reading) {
    // Keep age increasing even when a refresh fails.
    const quality = readingQuality(reading);
    return typeof quality === 'string' ? quality : quality.state;
}
function render() {
    const sensors = new Map((states.measurements?.sensors ?? []).map(sensor => [sensor.topic, sensor]));
    for (const [key, topic] of Object.entries(sensorKeys)) {
        if (key === 'top' || key === 'middle' || key === 'bottom') {
            renderValue('2000-' + key, sensors.get(topic));
        } else {
            renderValue(key, sensors.get(topic));
        }
    }
    for (const room of states.measurements?.rooms ?? []) {
        renderValue(room.id + '-measured', room);
        renderValue(room.id + '-target', room, 'setpoint');
    }
    for (const item of states.pumps?.pumps ?? []) {
        const el = pumpViews.get(item.id);
        if (!el) continue;
        const quality = qualityState(item);
        const known = typeof item.running === 'boolean';
        el.dataset.quality = known ? quality : 'unknown';
        el.dataset.state = known ? (item.running ? 'on' : 'off') : 'unknown';
        setText(el.querySelector('.td-pump-state'), known ?
            `${quality === 'fresh' ? '' : 'Zadnje: '}${item.running ? 'Radi' : 'Miruje'}` : 'Nema podatka');
        renderClock(el.querySelector('time'), item, quality);
    }
    setText(section.querySelector('[data-status]'), demo ? 'Demonstracija · testna mjerenja' :
        [errors.measurements && 'Osvježavanje mjerenja nije uspjelo.', errors.pumps && 'Osvježavanje pumpi nije uspjelo.'].filter(Boolean).join(' ') ||
        (states.measurements && states.pumps ? 'Automatsko osvježavanje' : 'Učitavanje mjerenja…'));
}

function accept(kind, raw, fetchedAt) {
    try {
        states[kind] = kind === 'measurements' ? parseMeasurements(raw, { fetchedAt }) : parsePumps(raw, { fetchedAt });
        errors[kind] = false;
    } catch {
        errors[kind] = true;
    }
    render();
}

async function refresh(kind, loader) {
    if (requests.has(kind)) return;
    requests.add(kind);
    try {
        states[kind] = await loader({ signal: AbortSignal.timeout(12000) });
        errors[kind] = false;
    } catch {
        errors[kind] = true;
    } finally {
        requests.delete(kind);
        render();
    }
}

let loadingWeather = false;
async function refreshWeather() {
    if (loadingWeather) return;
    loadingWeather = true;
    try {
        const weather = await loadWeather({ signal: AbortSignal.timeout(12000) });
        const current = weather.snapshot;
        const { label, icon } = weatherCondition(current.weatherCode, current.isDay);
        section.querySelector('[data-weather-icon]').textContent = weatherIcons[icon] ?? '◌';
        section.querySelector('[data-weather-status]').textContent = label;
        section.querySelector('[data-forecast-temp]').textContent = Number.isFinite(current.temperature) ?
            `Prognoza ${number.format(current.temperature)} °C` : '';
    } catch {
        section.querySelector('[data-weather-icon]').textContent = '◌';
        section.querySelector('[data-weather-status]').textContent = 'Vrijeme nedostupno';
        section.querySelector('[data-forecast-temp]').textContent = '';
    } finally {
        loadingWeather = false;
    }
}

function refreshHouse() {
    if (demo) return;
    if (window.termoDomHouseBridge) {
        window.termoDomHouseBridge.refresh();
    } else {
        void refresh('measurements', loadMeasurements);
        void refresh('pumps', loadPumps);
    }
    void refreshWeather();
}

section.querySelector('[data-refresh]').addEventListener('click', refreshHouse);
for (const kind of ['measurements', 'pumps']) {
    document.addEventListener('termodom:' + kind, event => accept(kind, event.detail.payload, event.detail.fetchedAt));
    document.addEventListener('termodom:' + kind + '-error', () => { errors[kind] = true; render(); });
    const cached = window.termoDomHouseBridge?.[kind];
    if (cached) accept(kind, cached.payload, cached.fetchedAt);
    if (window.termoDomHouseBridge?.[kind + 'Error']) errors[kind] = true;
}
render();
enableDiagramZoom();
if (demo) {
    section.querySelector('[data-weather-icon]').textContent = '⛅';
    section.querySelector('[data-weather-status]').textContent = 'Djelomice oblačno · primjer';
    section.querySelector('[data-forecast-temp]').textContent = 'Prognoza 8,1 °C';
}
if (!demo) {
    if (!window.termoDomHouseBridge) {
        void refresh('measurements', loadMeasurements);
        void refresh('pumps', loadPumps);
        setInterval(() => refresh('measurements', loadMeasurements), 60000);
        setInterval(() => refresh('pumps', loadPumps), 10000);
    }
    void refreshWeather();
    setInterval(refreshWeather, 300000);
}
setInterval(render, 1000);
