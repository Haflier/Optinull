(async function () {
  'use strict';

  const $ = (id) => document.getElementById(id);

  // The Telegram bot sends a link like https://host/#s=<token>. The token sits after the "#", so
  // the browser never sends it in a request line, and it never reaches a server log. It travels
  // only in the Authorization header of the API calls below.
  const token = new URLSearchParams(window.location.hash.slice(1)).get('s');

  const EXPIRED = 'This map link is not valid or has expired. Send /solve vrp map to the bot for a new one.';

  function api(path, options) {
    const headers = Object.assign({}, options && options.headers);

    if (token) headers.Authorization = 'Bearer ' + token;

    return fetch(path, Object.assign({}, options, { headers }));
  }

  const fallback = { map: { latitude: 20, longitude: 0, zoom: 2 }, limits: { maxCustomers: 99 }, session: null };

  // Why the page cannot be used, or null when it can. Without a valid session nothing is submitted.
  let locked = null;

  async function loadConfig() {
    if (!token) {
      locked = 'Open this page from the link the Telegram bot sent you. Send /solve vrp map to the bot.';

      return fallback;
    }

    try {
      const response = await api('/api/config');

      if (response.ok) return await response.json();

      locked = response.status === 401 || response.status === 410
        ? EXPIRED
        : 'The server could not load the map settings (' + response.status + ').';
    } catch (error) {
      locked = 'The server could not be reached.';
    }

    return fallback;
  }

  const config = await loadConfig();

  const state = {
    mode: 'depot',
    depot: null,         // { marker }
    customers: [],       // { marker, demand: number | null }
    depotBad: false,
    badCustomers: new Set(),
    routeLayer: null,    // the drawn roads
    customerColor: [],   // truck colour of each customer after a solve
    stopInfo: [],        // "Truck 2, stop 3 of 4" of each customer after a solve
    submitted: null,     // the problem as it was when Solve was pressed
    drawnJob: null,      // the job whose routes are on the map
    resultShown: false
  };

  // One colour per truck, then they repeat. Chosen to stay apart on the map tiles.
  const PALETTE = [
    '#e6194b', '#3cb44b', '#4363d8', '#f58231', '#911eb4',
    '#008080', '#9a6324', '#f032e6', '#808000', '#000075'
  ];

  // ---- Map ---------------------------------------------------------------------------------

  const map = L.map('map').setView([config.map.latitude, config.map.longitude], config.map.zoom);

  L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {
    maxZoom: 19,
    attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
  }).addTo(map);

  state.routeLayer = L.layerGroup().addTo(map);

  function pinIcon(kind, label, demand, invalid, color) {
    const badge = demand === undefined
      ? ''
      : '<small>' + (Number.isFinite(demand) ? demand : '?') + '</small>';

    return L.divIcon({
      className: '',
      html: '<div class="pin ' + kind + (invalid ? ' invalid' : '') + '"' +
        (color && !invalid ? ' style="background:' + color + '"' : '') + '>' + label + badge + '</div>',
      iconSize: [32, 32],
      iconAnchor: [16, 16],
      popupAnchor: [0, -16]
    });
  }

  function popup(title, onRemove) {
    const box = document.createElement('div');
    box.className = 'popup';

    const text = document.createElement('div');
    text.textContent = title;

    const button = document.createElement('button');
    button.type = 'button';
    button.textContent = 'Remove';
    button.addEventListener('click', onRemove);

    box.append(text, button);

    return box;
  }

  function refreshMarkers() {
    if (state.depot) {
      state.depot.marker.setIcon(pinIcon('depot', 'D', undefined, state.depotBad));
    }

    state.customers.forEach((customer, index) => {
      customer.marker.setIcon(
        pinIcon('customer', String(index + 1), customer.demand, state.badCustomers.has(index),
          state.customerColor[index]));
    });
  }

  // ---- Depot and customers -------------------------------------------------------------------

  function placeDepot(latlng) {
    if (state.depot) {
      state.depot.marker.setLatLng(latlng);
    } else {
      const marker = L.marker(latlng, { draggable: true, icon: pinIcon('depot', 'D') }).addTo(map);

      marker.on('dragend', clearFeedback);
      marker.bindPopup(() => popup('Depot', removeDepot));
      state.depot = { marker };
    }

    clearFeedback();
    setMode('customer');
  }

  function removeDepot() {
    if (!state.depot) return;

    map.removeLayer(state.depot.marker);
    state.depot = null;
    clearFeedback();
    setMode('depot');
  }

  function newCustomerDemand() {
    const value = Number($('new-demand').value);

    return Number.isInteger(value) && value >= 1 ? value : 1;
  }

  function addCustomer(latlng) {
    if (state.customers.length >= config.limits.maxCustomers) {
      showMessage('error', ['At most ' + config.limits.maxCustomers + ' customers are allowed.']);
      return;
    }

    const marker = L.marker(latlng, { draggable: true, icon: pinIcon('customer', '?') }).addTo(map);
    const customer = { marker, demand: newCustomerDemand() };

    marker.on('dragend', () => {
      clearFeedback();
      renderList();
    });
    marker.bindPopup(() =>
      popup(
        'Customer ' + (state.customers.indexOf(customer) + 1) +
          (state.stopInfo[state.customers.indexOf(customer)]
            ? ' \u00b7 ' + state.stopInfo[state.customers.indexOf(customer)]
            : ''),
        () => removeCustomer(customer)));

    state.customers.push(customer);
    clearFeedback();
    renderList();
  }

  function removeCustomer(customer) {
    const index = state.customers.indexOf(customer);

    if (index < 0) return;

    map.removeLayer(customer.marker);
    state.customers.splice(index, 1);
    clearFeedback();
    renderList();
  }

  // ---- Customer list -------------------------------------------------------------------------

  function renderList() {
    const list = $('customer-list');

    list.replaceChildren();

    state.customers.forEach((customer, index) => {
      const row = document.createElement('li');

      if (state.badCustomers.has(index)) row.classList.add('invalid');

      const name = document.createElement('span');
      name.className = 'name';
      name.textContent = String(index + 1);

      const place = document.createElement('span');
      place.className = 'place';

      const at = customer.marker.getLatLng();
      place.textContent = at.lat.toFixed(4) + ', ' + at.lng.toFixed(4);

      const demand = document.createElement('input');
      demand.type = 'number';
      demand.inputMode = 'numeric';
      demand.min = '1';
      demand.step = '1';
      demand.value = customer.demand === null ? '' : String(customer.demand);
      demand.setAttribute('aria-label', 'Demand of customer ' + (index + 1));

      // Typing must not rebuild the list, or the field would lose focus on every key.
      demand.addEventListener('input', () => {
        customer.demand = demand.value.trim() === '' ? null : Number(demand.value);
        clearFeedback();
      });

      const remove = document.createElement('button');
      remove.type = 'button';
      remove.textContent = '\u2715';
      remove.setAttribute('aria-label', 'Remove customer ' + (index + 1));
      remove.addEventListener('click', () => removeCustomer(customer));

      row.append(name, place, demand, remove);
      list.append(row);
    });

    $('customer-count').textContent = state.customers.length > 0 ? '(' + state.customers.length + ')' : '';
    $('empty').hidden = state.customers.length > 0;

    refreshMarkers();
    updateSummary();
  }

  // ---- Fleet, summary, feedback --------------------------------------------------------------

  function readNumber(input) {
    const text = input.value.trim();

    return text === '' ? null : Number(text);
  }

  function updateSummary() {
    const demands = state.customers.map((customer) => customer.demand).filter(Number.isFinite);
    const total = demands.reduce((sum, value) => sum + value, 0);
    const trucks = readNumber($('vehicles'));
    const capacity = readNumber($('capacity'));
    const parts = [];

    if (state.customers.length > 0) parts.push('Total demand ' + total);

    if (Number.isFinite(trucks) && Number.isFinite(capacity)) {
      parts.push('fleet capacity ' + trucks * capacity + ' (' + trucks + ' \u00d7 ' + capacity + ')');
    }

    $('summary').textContent = parts.join(' \u00b7 ');
  }

  function showMessage(kind, lines) {
    const box = document.createElement('div');
    box.className = kind;

    if (lines.length === 1) {
      box.textContent = lines[0];
    } else {
      const list = document.createElement('ul');

      lines.forEach((line) => {
        const item = document.createElement('li');
        item.textContent = line;
        list.append(item);
      });

      box.append(list);
    }

    $('messages').replaceChildren(box);
  }

  // Any edit makes an earlier verdict stale.
  // keep: true for the actions that must not discard the result (Check problem, Solve).
  function clearFeedback(keep) {
    if (keep !== true) clearResult();

    const hadMarks = state.depotBad || state.badCustomers.size > 0;

    $('messages').replaceChildren();
    $('vehicles').classList.remove('invalid');
    $('capacity').classList.remove('invalid');
    state.depotBad = false;
    state.badCustomers.clear();

    if (hadMarks) renderList();
    else {
      refreshMarkers();
      updateSummary();
    }
  }

  function showErrors(errors) {
    errors.forEach((error) => {
      if (error.field === 'vehicles') $('vehicles').classList.add('invalid');
      else if (error.field === 'capacity') $('capacity').classList.add('invalid');
      else if (error.field === 'depot') state.depotBad = true;
      else {
        const match = /^customers\[(\d+)\]/.exec(error.field);

        if (match) state.badCustomers.add(Number(match[1]));
      }
    });

    renderList();
    showMessage('error', errors.map((error) => error.message));
  }

  // ---- Modes ---------------------------------------------------------------------------------

  function setMode(mode) {
    state.mode = mode;
    $('mode-depot').setAttribute('aria-pressed', String(mode === 'depot'));
    $('mode-customer').setAttribute('aria-pressed', String(mode === 'customer'));

    $('hint').textContent = mode === 'depot'
      ? 'Tap the map to place the depot, where every route starts and ends. Drag it to move it.'
      : 'Tap the map to add a customer. Drag a pin to move it; tap a pin to remove it.';
  }

  map.on('click', (event) => {
    if (locked) return;

    // wrap() keeps the longitude in -180..180 when the map has been panned across the date line.
    const latlng = event.latlng.wrap();

    if (state.mode === 'depot') placeDepot(latlng);
    else addCustomer(latlng);
  });

  $('mode-depot').addEventListener('click', () => setMode('depot'));
  $('mode-customer').addEventListener('click', () => setMode('customer'));
  $('vehicles').addEventListener('input', clearFeedback);
  $('capacity').addEventListener('input', clearFeedback);

  // ---- Submit --------------------------------------------------------------------------------

  function buildRequest() {
    const point = (marker) => {
      const at = marker.getLatLng().wrap();

      return { latitude: at.lat, longitude: at.lng };
    };

    return {
      depot: state.depot ? point(state.depot.marker) : null,
      customers: state.customers.map((customer) => ({
        ...point(customer.marker),
        demand: customer.demand
      })),
      vehicles: readNumber($('vehicles')),
      capacity: readNumber($('capacity'))
    };
  }

  async function validate() {
    clearFeedback(true);

    const button = $('validate');
    button.disabled = true;

    let result;

    try {
      const response = await api('/api/vrp/validate', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(buildRequest())
      });

      if (response.status === 401 || response.status === 410) {
        lock(EXPIRED);
        return;
      }

      if (response.status === 413) throw new Error('The problem is too large to send.');
      if (!response.ok) throw new Error('The server could not read the problem (' + response.status + ').');

      result = await response.json();
    } catch (error) {
      showMessage('error', [
        error instanceof TypeError ? 'The server could not be reached.' : error.message
      ]);

      return;
    } finally {
      button.disabled = locked !== null;
    }

    if (!result.valid) {
      showErrors(result.errors);
      return;
    }

    const summary = result.summary;

    showMessage('ok', [
      'The problem is valid: ' + summary.customers + ' customers, total demand ' + summary.totalDemand +
      ', at least ' + summary.minVehicles + ' trucks needed. Press Solve to optimize.'
    ]);
  }

  $('validate').addEventListener('click', validate);

  // ---- Solve ---------------------------------------------------------------------------------

  let pollTimer = null;

  function stopPolling() {
    if (pollTimer !== null) {
      window.clearTimeout(pollTimer);
      pollTimer = null;
    }
  }

  function applyLock() {
    const banner = $('locked');

    banner.hidden = locked === null;
    banner.textContent = locked || '';

    ['solve', 'validate', 'clear', 'mode-depot', 'mode-customer', 'vehicles', 'capacity', 'new-demand']
      .forEach((id) => { $(id).disabled = locked !== null; });

    $('cancel-job').hidden = true;
  }

  function lock(reason) {
    locked = reason;
    stopPolling();
    applyLock();
  }

  // While a job is queued or running the Solve button waits and Cancel run takes its place.
  function setRunning(active) {
    $('solve').disabled = active || locked !== null;
    $('cancel-job').hidden = !active;
  }

  function kilometers(meters) {
    return (meters / 1000).toFixed(1) + ' km';
  }

  function plural(count, word) {
    return count + ' ' + word + (count === 1 ? '' : 's');
  }

  function renderStatus(status) {
    const box = $('result');
    const active = status.phase === 'Queued' || status.phase === 'Running';

    setRunning(active);
    box.replaceChildren();
    box.classList.remove('failed');
    box.hidden = status.phase === 'Idle';
    state.resultShown = !active && status.phase !== 'Idle';

    if (status.phase === 'Idle') return;

    const title = document.createElement('div');
    title.className = 'result-title';
    box.append(title);

    if (active) {
      const percent = Math.round(status.progress * 100);

      title.textContent = status.phase === 'Queued'
        ? 'Waiting in the queue...'
        : 'Optimizing' + (percent > 0 ? ' (' + percent + '%)' : '...');

      if (percent > 0) {
        const bar = document.createElement('progress');
        bar.max = 100;
        bar.value = percent;
        box.append(bar);
      }

      return;
    }

    if (status.phase === 'Cancelled') {
      title.textContent = 'The optimization was cancelled.';
      return;
    }

    if (status.phase === 'Failed' || !status.solution) {
      box.classList.add('failed');
      title.textContent = 'The optimization did not finish. Details were sent to your Telegram chat. ' +
        'You can change the problem and press Solve again.';
      return;
    }

    const solution = status.solution;

    title.textContent = solution.solver + ': ' + kilometers(solution.totalDistanceMeters) + ' in total, ' +
      plural(solution.routes.length, 'truck') + ' used.';

    const objective = document.createElement('div');
    objective.className = 'hint';
    objective.textContent = 'Objective value (total road distance): ' +
      Math.round(solution.totalDistanceMeters) + ' m';
    box.append(objective);

    const list = document.createElement('ul');
    list.className = 'trucks';

    solution.routes.forEach((route, index) => {
      const item = document.createElement('li');
      const swatch = document.createElement('span');
      const text = document.createElement('span');

      swatch.className = 'swatch';
      swatch.style.background = PALETTE[index % PALETTE.length];

      text.textContent = 'Truck ' + (index + 1) + ': customers ' +
        route.customers.map((customer) => customer + 1).join(' \u2192 ') +
        ' \u00b7 load ' + route.load + ' of ' + route.capacity +
        ' (' + (route.capacity - route.load) + ' left) \u00b7 ' + kilometers(route.distanceMeters);

      item.append(swatch, text);
      list.append(item);
    });

    box.append(list);

    showSolution(status);
  }

  // ---- Routes on the map ---------------------------------------------------------------------

  function clearResult() {
    state.routeLayer.clearLayers();
    state.customerColor = [];
    state.stopInfo = [];
    state.drawnJob = null;

    if (state.resultShown) {
      $('result').hidden = true;
      state.resultShown = false;
    }
  }

  function addNote(text) {
    const note = document.createElement('p');

    note.className = 'hint';
    note.textContent = text;
    $('result').append(note);

    return note;
  }

  function showSolution(status) {
    if (state.drawnJob === status.jobId) return;

    // The customer numbers in the solution are positions in the list that was submitted. If the
    // problem was edited since, they no longer point at the same pins, so nothing is drawn.
    if (state.submitted !== JSON.stringify(buildRequest())) {
      addNote('The problem was changed after it was submitted, so these routes are not drawn.');
      return;
    }

    state.drawnJob = status.jobId;

    status.solution.routes.forEach((route, index) => {
      route.customers.forEach((customer, stop) => {
        state.customerColor[customer] = PALETTE[index % PALETTE.length];
        state.stopInfo[customer] = 'Truck ' + (index + 1) + ', stop ' + (stop + 1) + ' of ' + route.customers.length;
      });
    });

    renderList();
    loadGeometry(status.jobId, status.solution);
  }

  async function loadGeometry(jobId, solution) {
    const loading = addNote('Loading the roads...');
    let routes = null;

    try {
      const response = await api('/api/vrp/geometry');

      if (response.status === 401 || response.status === 410) {
        lock(EXPIRED);
        return;
      }

      if (response.ok) {
        const data = await response.json();

        if (data.jobId === jobId) routes = data.routes;
      }
    } catch (error) {
      // The routes are still listed; the roads are drawn as straight lines below.
    }

    // The user edited or re-solved while the roads were loading.
    if (state.drawnJob !== jobId) return;

    loading.remove();
    drawRoutes(solution, routes);
  }

  function drawRoutes(solution, roads) {
    const depot = state.depot.marker.getLatLng();
    let missing = 0;

    state.routeLayer.clearLayers();

    solution.routes.forEach((route, index) => {
      const color = PALETTE[index % PALETTE.length];
      const road = roads && roads[index] && roads[index].points ? roads[index].points : null;

      if (road) {
        L.polyline(road, { color, weight: 5, opacity: 0.85 }).addTo(state.routeLayer);
        return;
      }

      // No road for this truck: show the visiting order as a dashed straight line instead.
      missing++;

      const stops = route.customers.map((customer) => state.customers[customer].marker.getLatLng());

      L.polyline([depot, ...stops, depot], { color, weight: 4, opacity: 0.8, dashArray: '8 8' })
        .addTo(state.routeLayer);
    });

    if (missing > 0) {
      addNote(
        'The roads of ' + plural(missing, 'truck') + ' could not be loaded, so ' +
        (missing === 1 ? 'its dashed line shows' : 'their dashed lines show') +
        ' only the visiting order. The routes and distances above are still correct.');
    }

    const bounds = state.routeLayer.getLayers().reduce(
      (all, layer) => all.extend(layer.getBounds()),
      L.latLngBounds([depot]));

    map.fitBounds(bounds, { padding: [30, 30] });
  }

  // initial: the page was just opened. Only a job still in progress is worth showing then.
  async function poll(initial) {
    pollTimer = null;

    let response;

    try {
      response = await api('/api/vrp/status');
    } catch (error) {
      // The connection may be down for a moment; keep trying while a job is running.
      if (!initial) pollTimer = window.setTimeout(poll, 3000);
      return;
    }

    if (response.status === 401 || response.status === 410) {
      lock(EXPIRED);
      return;
    }

    if (!response.ok) {
      if (!initial) pollTimer = window.setTimeout(poll, 3000);
      return;
    }

    const status = await response.json();
    const active = status.phase === 'Queued' || status.phase === 'Running';

    if (active || !initial) renderStatus(status);

    if (active) pollTimer = window.setTimeout(poll, 1000);
  }

  async function solve() {
    clearResult();
    clearFeedback(true);
    stopPolling();

    $('solve').disabled = true;

    let response;
    let body;

    try {
      const request = JSON.stringify(buildRequest());

      state.submitted = request;

      response = await api('/api/vrp/solve', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: request
      });

      body = await response.json().catch(() => null);
    } catch (error) {
      $('solve').disabled = locked !== null;
      showMessage('error', ['The server could not be reached.']);
      return;
    }

    if (response.status === 401 || response.status === 410) {
      lock(EXPIRED);
      return;
    }

    if (response.status === 400 && body && Array.isArray(body.errors)) {
      $('solve').disabled = false;
      showErrors(body.errors);
      return;
    }

    if (!response.ok || !body || !body.phase) {
      $('solve').disabled = false;
      showMessage('error', [
        body && body.error
          ? body.error
          : response.status === 413
            ? 'The problem is too large to send.'
            : 'The server could not start the optimization (' + response.status + ').'
      ]);
      return;
    }

    renderStatus(body);
    pollTimer = window.setTimeout(poll, 1000);
  }

  async function cancelRun() {
    const button = $('cancel-job');

    button.disabled = true;

    try {
      const response = await api('/api/vrp/cancel', { method: 'POST' });

      if (response.status === 401 || response.status === 410) {
        lock(EXPIRED);
        return;
      }

      // Polling carries on and shows the final state once the job has stopped.
      if (response.ok) renderStatus(await response.json());
    } catch (error) {
      showMessage('error', ['The server could not be reached.']);
    } finally {
      button.disabled = false;
    }
  }

  $('solve').addEventListener('click', solve);
  $('cancel-job').addEventListener('click', cancelRun);

  $('clear').addEventListener('click', () => {
    if (!state.depot && state.customers.length === 0) return;
    if (!window.confirm('Remove the depot and all customers?')) return;

    if (state.depot) map.removeLayer(state.depot.marker);
    state.customers.forEach((customer) => map.removeLayer(customer.marker));

    state.depot = null;
    state.customers = [];
    clearFeedback();
    renderList();
    setMode('depot');
  });

  if (config.session) {
    $('session-info').textContent = 'Algorithm: ' + config.session.solver +
      (config.session.iterations ? ', ' + config.session.iterations + ' iterations' : '');
  }

  applyLock();
  setMode('depot');
  renderList();

  // After a reload, pick up a job that is still in progress.
  if (!locked) poll(true);
})();
