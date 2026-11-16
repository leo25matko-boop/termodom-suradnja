// Public read endpoints used by the existing m.php page. This module performs
// no controller writes and does not import browser cookies across origins.
export const SOURCE = 'https://automatiziraj.se/centralno/php/m.php';
export const WEATHER_SOURCE = 'https://open-meteo.com/en/docs';
export const WEATHER_LOCATION = Object.freeze({name: 'Ozalj', latitude: 45.61293, longitude: 15.47771, timezone: 'Europe/Zagreb'});

export const SENSORS = Object.freeze([
  {topic: 'house/puffer/top', name: 'Pufer — vrh'},
  {topic: 'house/puffer/midd', name: 'Pufer — sredina'},
  {topic: 'house/puffer/bottom', name: 'Pufer — dno'},
  {topic: 'house/kotal/temp', name: 'Kotao'},
  {topic: 'house/dhw/temp', name: 'Sanitarna voda'},
  {topic: 'house/solar/temp', name: 'Solarni sustav'},
  {topic: 'house/outdoor/temp', name: 'Vanjska temperatura'},
  {topic: 'house/polaz_priz/temp', name: 'Polaz prizemlja'},
  {topic: 'house/polaz_kat/temp', name: 'Polaz kata'},
  {topic: 'house/polaz_priz2/temp', name: 'Polaz prizemlja 2'},
  {topic: 'house/sensor1/temp', name: 'Senzor 1'},
].map(Object.freeze));

export const PUMPS = Object.freeze([
  {id: 'kotao', name: 'Pumpa kotla'},
  {id: 'prizemlje', name: 'Pumpa prizemlja'},
  {id: 'kat', name: 'Pumpa kata'},
  {id: 'sanitarna', name: 'Pumpa sanitarne vode'},
  {id: 'solar', name: 'Solarna pumpa'},
].map(Object.freeze));

const ROOMS = Object.freeze([{id: 'prizemlje', name: 'Prizemlje'}, {id: 'kat', name: 'Kat'}].map(Object.freeze));
const DELAYED_AFTER = 3 * 60_000;
const STALE_AFTER = 10 * 60_000;
const sensorNames = new Map(SENSORS.map(sensor => [sensor.topic, sensor.name]));

function finite(value) {
  if (typeof value === 'number') return Number.isFinite(value) ? value : null;
  if (typeof value !== 'string' || !/^[+-]?(?:\d+(?:\.\d+)?|\.\d+)$/.test(value.trim())) return null;
  const number = Number(value.trim());
  return Number.isFinite(number) ? number : null;
}

// Both timestamps come from the same server wall clock. Compare clock fields
// without interpreting those strings in the user's or server's time zone.
function wallTime(value) {
  if (typeof value !== 'string') return null;
  const match = /^(\d{4}-\d{2}-\d{2})[ T](\d{2}:\d{2}:\d{2})$/.exec(value);
  if (!match) return null;
  const iso = `${match[1]}T${match[2]}.000Z`;
  const date = new Date(iso);
  return Number.isFinite(date.getTime()) && date.toISOString() === iso ? date.getTime() : null;
}

function readAge(timestamp, serverTime) {
  const sample = wallTime(timestamp);
  const source = wallTime(serverTime);
  return sample === null || source === null || source < sample ? null : source - sample;
}

function validTime(timestamp) { return wallTime(timestamp) === null ? null : timestamp; }

/** The age continues increasing after receipt; fetching again cannot make old data new. */
export function readingQuality(reading, nowMs = Date.now()) {
  const base = reading?.ageMs;
  const fetched = reading?.fetchedAt;
  if (!Number.isFinite(base) || base < 0) return {state: 'unknown', ageMs: null};
  const ageMs = base + (Number.isFinite(fetched) && Number.isFinite(nowMs) ? Math.max(0, nowMs - fetched) : 0);
  const state = ageMs <= DELAYED_AFTER ? 'fresh' : ageMs <= STALE_AFTER ? 'delayed' : 'stale';
  return {state, ageMs};
}

function array(raw, field) {
  if (!Array.isArray(raw[field])) throw new Error(`Izvor nije vratio očekivani popis: ${field}.`);
  return raw[field];
}

function responseStamp(raw, fetchedAt) {
  if (raw?.success !== true) throw new Error('Izvor grijanja nije potvrdio očitanja.');
  return {serverTime: validTime(raw.server_time), fetchedAt: Number.isFinite(fetchedAt) ? fetchedAt : Date.now()};
}

function newest(rows, key, time) {
  const result = new Map();
  for (const row of rows) {
    if (!row || typeof row[key] !== 'string' || !row[key]) continue;
    const previous = result.get(row[key]);
    if (!previous || (wallTime(row[time]) ?? -Infinity) >= (wallTime(previous[time]) ?? -Infinity)) result.set(row[key], row);
  }
  return result;
}

function measurement(row) {
  return {
    topic: row.topic,
    name: sensorNames.get(row.topic) ?? row.topic,
    temperature: finite(row.temperature),
    receivedAt: validTime(row.received_at),
  };
}

function thermostat(row) {
  return {id: row.room, temperature: finite(row.temperature), setpoint: finite(row.setpoint), receivedAt: validTime(row.received_at)};
}

export function parseMeasurements(raw, {fetchedAt} = {}) {
  const stamp = responseStamp(raw, fetchedAt);
  const latest = newest(array(raw, 'latest'), 'topic', 'received_at');
  const labels = new Map(SENSORS.map(sensor => [sensor.topic, sensor.name]));
  for (const topic of latest.keys()) {
    // Current and target thermostat values are rendered authoritatively from
    // room_latest; their MQTT duplicates remain available in measurement history.
    if (!labels.has(topic) && !/^house\/room\/(kat|prizemlje)\/(temp|setpoint)$/.test(topic)) labels.set(topic, topic);
  }
  const sensors = [...labels].map(([topic, name]) => {
    const value = measurement(latest.get(topic) ?? {topic});
    return {...value, name, fetchedAt: stamp.fetchedAt, ageMs: value.temperature === null ? null : readAge(value.receivedAt, stamp.serverTime)};
  });
  const roomRows = newest(array(raw, 'room_latest'), 'room', 'received_at');
  const rooms = ROOMS.map(room => {
    const value = thermostat(roomRows.get(room.id) ?? {room: room.id});
    return {...room, ...value, fetchedAt: stamp.fetchedAt, ageMs: value.temperature === null && value.setpoint === null ? null : readAge(value.receivedAt, stamp.serverTime)};
  });
  const history = array(raw, 'history').filter(row => typeof row?.topic === 'string').map(measurement);
  const roomHistory = array(raw, 'room_history').filter(row => typeof row?.room === 'string').map(thermostat);
  return {...stamp, sensors, rooms, history, roomHistory};
}

export function parsePumps(raw, {fetchedAt} = {}) {
  const stamp = responseStamp(raw, fetchedAt);
  const rows = newest(array(raw, 'pumps'), 'pump', 'updated_at');
  const pumps = PUMPS.map(pump => {
    const row = rows.get(pump.id);
    const state = finite(row?.state);
    const running = state === 1 ? true : state === 0 ? false : null;
    const updatedAt = validTime(row?.updated_at);
    return {...pump, running, updatedAt, fetchedAt: stamp.fetchedAt, ageMs: running === null ? null : readAge(updatedAt, stamp.serverTime)};
  });
  return {...stamp, pumps};
}

/** Croatian labels for documented WMO/Open-Meteo conditions; use the icon key independently. */
export function weatherCondition(code, isDay = true) {
  if (code === 0) return {label: 'Vedro', icon: isDay === false ? 'moon' : 'sun'};
  if (code === 1) return {label: 'Pretežno vedro', icon: isDay === false ? 'moon-cloud' : 'sun-cloud'};
  if (code === 2) return {label: 'Djelomice oblačno', icon: isDay === false ? 'moon-cloud' : 'sun-cloud'};
  if (code === 3) return {label: 'Oblačno', icon: 'cloud'};
  if ([45, 48].includes(code)) return {label: 'Magla', icon: 'fog'};
  if ([51, 53, 55, 56, 57].includes(code)) return {label: 'Rosulja', icon: 'rain'};
  if ([61, 63, 65, 66, 67].includes(code)) return {label: 'Kiša', icon: 'rain'};
  if ([71, 73, 75, 77, 85, 86].includes(code)) return {label: 'Snijeg', icon: 'snow'};
  if ([80, 81, 82].includes(code)) return {label: 'Pljuskovi', icon: 'rain'};
  if ([95, 96, 97, 99].includes(code)) return {label: 'Grmljavina', icon: 'storm'};
  return {label: 'Nepoznati uvjeti', icon: 'unknown'};
}

function localWeatherTime(value, daily = false) {
  if (typeof value !== 'string') return null;
  if (daily) return wallTime(`${value} 00:00:00`) === null ? null : value;
  return /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}$/.test(value) && wallTime(`${value}:00`) !== null ? value : null;
}

function weatherSeries(data, field, index) {
  return Array.isArray(data?.[field]) ? data[field][index] : null;
}

export function parseWeather(raw) {
  if (!raw || raw.error === true || raw.timezone !== WEATHER_LOCATION.timezone || !raw.current) throw new Error('Vremenska prognoza za Ozalj nije dostupna.');
  const current = raw.current;
  const time = localWeatherTime(current.time);
  if (time === null) throw new Error('Vremenska prognoza nema valjano vrijeme očitanja.');
  const isDay = current.is_day === 1 ? true : current.is_day === 0 ? false : null;
  const daily = raw.daily;
  const hourly = raw.hourly;
  const forecast = (Array.isArray(daily?.time) ? daily.time : []).map((date, index) => ({
    date: localWeatherTime(date, true), weatherCode: finite(weatherSeries(daily, 'weather_code', index)),
    high: finite(weatherSeries(daily, 'temperature_2m_max', index)), low: finite(weatherSeries(daily, 'temperature_2m_min', index)),
    precipitationProbability: finite(weatherSeries(daily, 'precipitation_probability_max', index)),
    sunrise: localWeatherTime(weatherSeries(daily, 'sunrise', index)), sunset: localWeatherTime(weatherSeries(daily, 'sunset', index)),
  })).filter(day => day.date !== null);
  const hourlyForecast = (Array.isArray(hourly?.time) ? hourly.time : []).map((value, index) => ({
    time: localWeatherTime(value), temperature: finite(weatherSeries(hourly, 'temperature_2m', index)),
    precipitationProbability: finite(weatherSeries(hourly, 'precipitation_probability', index)),
    weatherCode: finite(weatherSeries(hourly, 'weather_code', index)),
  })).filter(hour => hour.time !== null);
  return {
    location: WEATHER_LOCATION.name, timezone: WEATHER_LOCATION.timezone, time,
    snapshot: {
      temperature: finite(current.temperature_2m), feelsLike: finite(current.apparent_temperature), humidity: finite(current.relative_humidity_2m),
      weatherCode: finite(current.weather_code), isDay, windSpeed: finite(current.wind_speed_10m), pressure: finite(current.pressure_msl),
      precipitation: finite(current.precipitation),
    },
    forecast, hourly: hourlyForecast,
  };
}

async function getJson(url, {signal, fetchImpl = globalThis.fetch} = {}) {
  const abort = new AbortController();
  const forward = () => abort.abort(signal.reason);
  if (signal?.aborted) forward();
  else signal?.addEventListener('abort', forward, {once: true});
  const timer = setTimeout(() => abort.abort(), 15_000);
  try {
    const response = await fetchImpl(url.href, {method: 'GET', cache: 'no-store', redirect: 'error', signal: abort.signal, headers: {Accept: 'application/json'}});
    if (!response.ok) throw new Error(`Izvor je vratio HTTP ${response.status}.`);
    return await response.json();
  } finally {
    clearTimeout(timer);
    signal?.removeEventListener('abort', forward);
  }
}

function measurementUrl(action, options) {
  const loopback = globalThis.location?.protocol === 'http:' && globalThis.location?.hostname === '127.0.0.1';
  const localRelay = options?.localRelay ?? globalThis.document?.body?.hasAttribute('data-house-local');
  const url = loopback && localRelay ? new URL('/m.php', globalThis.location.origin) : new URL(SOURCE);
  url.searchParams.set('action', action);
  return url;
}

export async function loadMeasurements(options) { return parseMeasurements(await getJson(measurementUrl('data', options), options)); }
export async function loadPumps(options) { return parsePumps(await getJson(measurementUrl('pumps', options), options)); }
export async function loadWeather(options) {
  const {latitude, longitude, timezone} = WEATHER_LOCATION;
  const url = new URL('https://api.open-meteo.com/v1/forecast');
  url.search = new URLSearchParams({
    latitude, longitude, timezone, forecast_days: '5',
    current: 'temperature_2m,apparent_temperature,relative_humidity_2m,weather_code,is_day,wind_speed_10m,pressure_msl,precipitation',
    hourly: 'temperature_2m,precipitation_probability,weather_code',
    daily: 'weather_code,temperature_2m_max,temperature_2m_min,precipitation_probability_max,sunrise,sunset',
  });
  return parseWeather(await getJson(url, options));
}
