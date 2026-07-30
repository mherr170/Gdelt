(() => {
  const logEl = document.getElementById('log');
  const emptyEl = document.getElementById('empty');
  const cardsEl = document.getElementById('cards');
  const toolbarEl = document.querySelector('.toolbar');
  const dot = document.getElementById('dot');
  const statusText = document.getElementById('statusText');
  const countAll = document.getElementById('count-all');

  const MAX_ROWS = 500;

  const state = {
    activeWidget: '*',
    activeLevels: new Set(['INFO', 'SUCCESS', 'WARN', 'ERROR']),
    widgets: new Map(), // widget -> latest event
    total: 0,
    knownWidgetChips: new Set(),
  };

  function timeAgo(ts) {
    const secs = Math.max(0, Math.floor((Date.now() - new Date(ts).getTime()) / 1000));
    if (secs < 5) return 'just now';
    if (secs < 60) return `${secs}s ago`;
    const mins = Math.floor(secs / 60);
    if (mins < 60) return `${mins}m ago`;
    const hrs = Math.floor(mins / 60);
    if (hrs < 24) return `${hrs}h ago`;
    return `${Math.floor(hrs / 24)}d ago`;
  }

  function fmtTime(ts) {
    const d = new Date(ts);
    return d.toLocaleTimeString('en-US', { hour12: false });
  }

  function ensureWidgetChip(widget) {
    if (state.knownWidgetChips.has(widget)) return;
    state.knownWidgetChips.add(widget);
    const chip = document.createElement('div');
    chip.className = 'chip active';
    chip.dataset.widget = widget;
    chip.innerHTML = `${widget}<span class="n" id="count-${widget}">0</span>`;
    chip.addEventListener('click', () => selectWidget(widget));
    // Insert right after the "All" chip.
    toolbarEl.insertBefore(chip, toolbarEl.querySelector('.sep'));
  }

  function selectWidget(widget) {
    state.activeWidget = widget;
    document.querySelectorAll('.chip[data-widget]').forEach(c => {
      c.classList.toggle('active', c.dataset.widget === widget);
    });
    applyFilter();
  }

  function applyFilter() {
    document.querySelectorAll('#log .row').forEach(row => {
      const widgetOk = state.activeWidget === '*' || row.dataset.widget === state.activeWidget;
      const levelOk = state.activeLevels.has(row.dataset.level);
      row.style.display = (widgetOk && levelOk) ? '' : 'none';
    });
  }

  function bumpCount(el) {
    if (!el) return;
    el.textContent = (parseInt(el.textContent, 10) || 0) + 1;
  }

  function updateCard(evt) {
    state.widgets.set(evt.widget, evt);
    renderCards();
  }

  function renderCards() {
    const widgets = [...state.widgets.entries()].sort((a, b) => a[0].localeCompare(b[0]));
    cardsEl.innerHTML = '';
    for (const [widget, evt] of widgets) {
      const card = document.createElement('div');
      card.className = `card lvl-${evt.level}`;
      card.innerHTML = `
        <div class="name">${widget}</div>
        <div class="msg" title="${escapeHtml(evt.message)}">${escapeHtml(evt.message)}</div>
        <div class="ago" data-ts="${evt.timestamp}">${timeAgo(evt.timestamp)}</div>`;
      cardsEl.appendChild(card);
    }
  }

  function escapeHtml(s) {
    const div = document.createElement('div');
    div.textContent = s;
    return div.innerHTML;
  }

  function addRow(evt) {
    if (emptyEl) { emptyEl.remove(); }

    ensureWidgetChip(evt.widget);

    const row = document.createElement('div');
    row.className = `row lvl-${evt.level}`;
    row.dataset.widget = evt.widget;
    row.dataset.level = evt.level;
    row.innerHTML = `
      <span class="ts">${fmtTime(evt.timestamp)}</span>
      <span class="widget">${evt.widget}</span>
      <span class="lvl">${evt.level}</span>
      <span class="msg">${escapeHtml(evt.message)}</span>`;

    const widgetOk = state.activeWidget === '*' || evt.widget === state.activeWidget;
    const levelOk = state.activeLevels.has(evt.level);
    row.style.display = (widgetOk && levelOk) ? '' : 'none';

    const wasAtBottom = logEl.scrollHeight - logEl.scrollTop - logEl.clientHeight < 40;
    logEl.appendChild(row);
    while (logEl.children.length > MAX_ROWS) logEl.removeChild(logEl.firstChild);
    if (wasAtBottom) logEl.scrollTop = logEl.scrollHeight;

    state.total++;
    countAll.textContent = state.total;
    bumpCount(document.getElementById(`count-${evt.widget}`));

    updateCard(evt);
  }

  function setConnected(connected) {
    dot.classList.toggle('live', connected);
    statusText.textContent = connected ? 'live' : 'reconnecting…';
  }

  // Toolbar wiring — "All" chip + level chips (widget chips added dynamically).
  document.querySelector('.chip[data-widget="*"]').addEventListener('click', () => selectWidget('*'));
  document.querySelectorAll('.chip[data-level]').forEach(chip => {
    chip.addEventListener('click', () => {
      const level = chip.dataset.level;
      chip.classList.toggle('active');
      if (chip.classList.contains('active')) state.activeLevels.add(level);
      else state.activeLevels.delete(level);
      applyFilter();
    });
  });
  document.getElementById('clearBtn').addEventListener('click', () => {
    logEl.innerHTML = '';
    state.total = 0;
    countAll.textContent = '0';
  });

  setInterval(() => {
    document.querySelectorAll('.card .ago').forEach(el => {
      el.textContent = timeAgo(el.dataset.ts);
    });
  }, 5000);

  async function loadRecent() {
    try {
      const res = await fetch('/api/recent');
      const events = await res.json();
      events.forEach(addRow);
    } catch { /* SSE will still populate live */ }
  }

  function connect() {
    const es = new EventSource('/events');
    es.onopen = () => setConnected(true);
    es.onerror = () => {
      setConnected(false);
      es.close();
      setTimeout(connect, 2000);
    };
    es.onmessage = (e) => {
      try { addRow(JSON.parse(e.data)); } catch { /* ignore malformed */ }
    };
  }

  loadRecent().then(connect);
})();
