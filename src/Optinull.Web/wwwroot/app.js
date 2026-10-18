(async function () {
  'use strict';

  const $ = (id) => document.getElementById(id);

  const fallback = { map: { latitude: 20, longitude: 0, zoom: 2 }, limits: { maxCustomers: 99 } };
  const config = await fetch('/api/config')
    .then((response) => (response.ok ? response.json() : fallback))
    .catch(() => fallback);

  const state = {
    mode: 'depot',
    depot: null,         // { marker }
    customers: [],       // { marker, demand: number | null }
    depotBad: false,
    badCustomers: new Set()
  };

  // ---- Map ---------------------------------------------------------------------------------

  const map = L.map('map').setView([config.map.latitude, config.map.longitude], config.map.zoom);

  L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {
    maxZoom: 19,
    attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
  }).addTo(map);

  function pinIcon(kind, label, demand, invalid) {
    const badge = demand === undefined
      ? ''
      : '<small>' + (Number.isFinite(demand) ? demand : '?') + '</small>';

    return L.divIcon({
      className: '',
      html: '<div class="pin ' + kind + (invalid ? ' invalid' : '') + '">' + label + badge + '</div>',
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
        pinIcon('customer', String(index + 1), customer.demand, state.badCustomers.has(index)));
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
      popup('Customer ' + (state.customers.indexOf(customer) + 1), () => removeCustomer(customer)));

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
  function clearFeedback() {
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
    clearFeedback();

    const button = $('validate');
    button.disabled = true;

    let result;

    try {
      const response = await fetch('/api/vrp/validate', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(buildRequest())
      });

      if (response.status === 413) throw new Error('The problem is too large to send.');
      if (!response.ok) throw new Error('The server could not read the problem (' + response.status + ').');

      result = await response.json();
    } catch (error) {
      showMessage('error', [
        error instanceof TypeError ? 'The server could not be reached.' : error.message
      ]);

      return;
    } finally {
      button.disabled = false;
    }

    if (!result.valid) {
      showErrors(result.errors);
      return;
    }

    const summary = result.summary;

    showMessage('ok', [
      'The problem is valid: ' + summary.customers + ' customers, total demand ' + summary.totalDemand +
      ', at least ' + summary.minVehicles + ' trucks needed. Optimization is connected in the next step.'
    ]);
  }

  $('validate').addEventListener('click', validate);

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

  setMode('depot');
  renderList();
})();
