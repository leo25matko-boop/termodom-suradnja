import test from 'node:test';
import assert from 'node:assert/strict';
import {loadMeasurements, loadPumps, loadWeather, parseMeasurements, parsePumps, parseWeather, readingQuality, weatherCondition} from './m-house-data.js';

const server_time = '2026-11-01 08:00:00';
const fetchedAt = 1000;
const data = {
  success: true, server_time,
  latest: [
    {topic: 'house/puffer/top', temperature: '32.77', received_at: '2026-11-01 07:59:00'},
    {topic: 'house/puffer/top', temperature: '11.90', received_at: '2026-11-01 07:58:00'},
    {topic: 'house/puffer/midd', temperature: '28.50', received_at: '2026-11-01 07:57:00'},
    {topic: 'house/puffer/bottom', temperature: '25.21', received_at: '2026-11-01 07:50:00'},
    {topic: 'house/outdoor/temp', temperature: '0', received_at: '2026-11-01 07:59:30'},
    {topic: 'house/solar/temp', temperature: ' ', received_at: '2026-11-01 07:59:30'},
    {topic: 'house/kotal/temp', temperature: '30.12', received_at: '2026-11-01 08:03:00'},
    {topic: 'house/polaz_priz2/temp', temperature: '19.12', received_at: '2026-10-30 08:03:00'},
    {topic: 'house/room/kat/temp', temperature: '31.0', received_at: '2026-11-01 07:59:30'},
    {topic: 'house/extra/temp', temperature: '14.18', received_at: '2026-11-01 07:59:00'},
  ],
  history: [{topic: 'house/outdoor/temp', temperature: '-0.75', received_at: '2026-10-31 23:30:00'}],
  room_latest: [{room: 'kat', temperature: '21.62', setpoint: '24.6', received_at: '2026-11-01 07:59:01'}],
  room_history: [{room: 'prizemlje', temperature: '21.25', setpoint: null, received_at: '2026-10-31 23:30:01'}],
};

test('live readings retain source timestamps and use accurate 3/10-minute states', () => {
  const snapshot = parseMeasurements(data, {fetchedAt});
  const readings = new Map(snapshot.sensors.map(row => [row.topic, row]));
  const state = (topic, now = fetchedAt) => readingQuality(readings.get(`house/${topic}`), now).state;
  assert.equal(readings.get('house/puffer/top').temperature, 32.77);
  assert.equal(state('puffer/top'), 'fresh');
  assert.equal(state('puffer/top', fetchedAt + 120_000), 'fresh');
  assert.equal(state('puffer/top', fetchedAt + 120_001), 'delayed');
  assert.equal(state('puffer/top', fetchedAt + 540_000), 'delayed');
  assert.equal(state('puffer/top', fetchedAt + 540_001), 'stale');
  assert.equal(state('puffer/midd'), 'fresh');
  assert.equal(state('puffer/bottom'), 'delayed');
  assert.equal(state('polaz_priz2/temp'), 'stale');
  assert.equal(state('kotal/temp'), 'unknown');
  assert.equal(state('solar/temp'), 'unknown');
  assert.equal(state('sensor1/temp'), 'unknown');
  assert.equal(state('outdoor/temp'), 'fresh');
  assert.equal(readings.get('house/outdoor/temp').temperature, 0);
  assert.equal(readings.get('house/extra/temp').name, 'house/extra/temp');
  assert.equal(readings.has('house/room/kat/temp'), false);
  assert.equal(snapshot.rooms.find(row => row.id === 'kat').temperature, 21.62);
  assert.equal(snapshot.rooms.find(row => row.id === 'kat').setpoint, 24.6);
  assert.equal(snapshot.rooms.find(row => row.id === 'prizemlje').temperature, null);
  assert.equal(snapshot.history[0].temperature, -0.75);
  assert.equal(snapshot.roomHistory[0].setpoint, null);
  assert.equal(parseMeasurements({...data, server_time: '2026-02-30 11:12:13'}).serverTime, null);
  assert.throws(() => parseMeasurements({...data, success: false}), /nije potvrdio/);
  assert.throws(() => parseMeasurements({...data, room_latest: null}), /room_latest/);
});

const pumps = {success: true, server_time, pumps: [
  {pump: 'kat', state: 0, updated_at: '2026-11-01 07:56:00'},
  {pump: 'prizemlje', state: 1, updated_at: '2026-11-01 07:59:00'},
  {pump: 'kotao', state: 7, updated_at: '2026-11-01 07:59:00'},
  {pump: 'solar', state: null, updated_at: '2026-11-01 07:59:00'},
]};

test('pumps distinguish reported off from an invalid or missing state', () => {
  const map = new Map(parsePumps(pumps, {fetchedAt}).pumps.map(row => [row.id, row]));
  assert.equal(map.get('kat').running, false);
  assert.equal(map.get('kat').updatedAt, '2026-11-01 07:56:00');
  assert.equal(readingQuality(map.get('kat'), fetchedAt).state, 'delayed');
  assert.equal(map.get('prizemlje').running, true);
  for (const id of ['kotao', 'solar', 'sanitarna']) {
    assert.equal(map.get(id).running, null);
    assert.equal(readingQuality(map.get(id), fetchedAt).state, 'unknown');
  }
});

const weather = {
  timezone: 'Europe/Zagreb',
  current: {time: '2026-11-01T08:00', temperature_2m: 6.1, apparent_temperature: 4.2, relative_humidity_2m: 82, weather_code: 0, is_day: 0, wind_speed_10m: 2.2, pressure_msl: 1018, precipitation: 0},
  daily: {time: ['2026-11-01', '2026-11-02'], weather_code: [45, 0], temperature_2m_max: [8, 10], temperature_2m_min: [4, 2], precipitation_probability_max: [0, 5], sunrise: ['2026-11-01T06:33', '2026-11-02T06:34'], sunset: ['2026-11-01T16:46', '2026-11-02T16:44']},
  hourly: {time: ['2026-11-01T08:00', '2026-11-01T09:00'], temperature_2m: [6.1, null], precipitation_probability: [5, 50], weather_code: [0, 61]},
};

test('weather preserves Ozalj local validity, zero precipitation, and daily/hour indexes', () => {
  const result = parseWeather(weather);
  assert.equal(result.location, 'Ozalj');
  assert.equal(result.snapshot.precipitation, 0);
  assert.equal(result.snapshot.isDay, false);
  assert.deepEqual(weatherCondition(result.snapshot.weatherCode, result.snapshot.isDay), {label: 'Vedro', icon: 'moon'});
  assert.equal(result.forecast[0].precipitationProbability, 0);
  assert.equal(result.forecast[1].sunset, '2026-11-02T16:44');
  assert.equal(result.hourly[1].temperature, null);
  assert.equal(result.hourly[1].precipitationProbability, 50);
  assert.throws(() => parseWeather({...weather, timezone: 'GMT'}));
});

test('network wrappers only request documented read endpoints and forecast coordinates', async () => {
  const requests = [];
  const fetchImpl = async (value, options) => {
    const url = new URL(value);
    requests.push({url, options});
    return Response.json(url.hostname === 'api.open-meteo.com' ? weather : url.searchParams.get('action') === 'pumps' ? pumps : data);
  };
  await loadMeasurements({fetchImpl});
  await loadPumps({fetchImpl});
  await loadWeather({fetchImpl});
  assert.equal(requests[0].url.href, 'https://automatiziraj.se/centralno/php/m.php?action=data');
  assert.equal(requests[1].url.href, 'https://automatiziraj.se/centralno/php/m.php?action=pumps');
  assert.equal(requests[2].url.hostname, 'api.open-meteo.com');
  assert.equal(requests[2].url.searchParams.get('latitude'), '45.61293');
  assert.equal(requests[2].url.searchParams.get('longitude'), '15.47771');
  assert.equal(requests[2].url.searchParams.get('timezone'), 'Europe/Zagreb');
  assert.ok(requests.every(({options}) => options.method === 'GET' && options.cache === 'no-store' && options.redirect === 'error'));
});

test('the same-origin relay is selected only by an explicit loopback opt-in', async () => {
  const requests = [];
  const fetchImpl = async url => { requests.push(url); return Response.json(pumps); };
  const oldLocation = Object.getOwnPropertyDescriptor(globalThis, 'location');
  const oldDocument = Object.getOwnPropertyDescriptor(globalThis, 'document');
  try {
    globalThis.location = new URL('http://127.0.0.1:8740/');
    globalThis.document = {body: {hasAttribute: () => false}};
    await loadPumps({fetchImpl});
    await loadPumps({fetchImpl, localRelay: true});
    globalThis.document = {body: {hasAttribute: name => name === 'data-house-local'}};
    await loadPumps({fetchImpl});
    globalThis.location = new URL('https://automatiziraj.se/centralno/php/m.php');
    await loadPumps({fetchImpl, localRelay: true});
  } finally {
    if (oldLocation) Object.defineProperty(globalThis, 'location', oldLocation); else delete globalThis.location;
    if (oldDocument) Object.defineProperty(globalThis, 'document', oldDocument); else delete globalThis.document;
  }
  assert.deepEqual(requests, [
    'https://automatiziraj.se/centralno/php/m.php?action=pumps',
    'http://127.0.0.1:8740/m.php?action=pumps',
    'http://127.0.0.1:8740/m.php?action=pumps',
    'https://automatiziraj.se/centralno/php/m.php?action=pumps',
  ]);
});
