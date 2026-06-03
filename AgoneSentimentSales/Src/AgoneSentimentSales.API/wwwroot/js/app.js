/* AG ONE Sentiment Sales — Professional SPA */
(function () {
  'use strict';

  const state = {
    views: [],
    currentView: 'tracker',
    jobId: null,
    charts: {},
    progressConn: null,
    extractConn: null,
    pollTimer: null,
    sourceCounts: {},
    progressLabels: [],
    progressData: []
  };

  const $ = (id) => document.getElementById(id);
  const esc = (s) => { const d = document.createElement('div'); d.textContent = s ?? ''; return d.innerHTML; };

  async function api(path, opts) {
    const r = await fetch(path, opts);
    if (!r.ok) {
      const t = await r.text();
      throw new Error(t || r.statusText);
    }
    const ct = r.headers.get('content-type') || '';
    return ct.includes('json') ? r.json() : r;
  }

  function toast(msg) {
    const el = document.createElement('div');
    el.className = 'toast';
    el.textContent = msg;
    document.body.appendChild(el);
    setTimeout(() => el.remove(), 4000);
  }

  function jobQuery() {
    return state.jobId ? '?jobId=' + state.jobId : '';
  }

  function offshoringPill(s) {
    const v = (s || '').toLowerCase();
    if (v === 'confirmed') return '<span class="pill pill-confirmed">Confirmed</span>';
    if (v === 'partial') return '<span class="pill pill-partial">Partial</span>';
    return '<span class="pill pill-none">None</span>';
  }

  function renderTable(headers, rows, searchId) {
    const id = searchId || 'tblSearch';
    let html = '<input type="search" class="table-search" id="' + id + '" placeholder="Filter rows…" />';
    html += '<div class="data-table-wrap"><table class="pro-table" id="' + id + 'Table"><thead><tr>';
    headers.forEach(h => { html += '<th>' + esc(h) + '</th>'; });
    html += '</tr></thead><tbody>';
    rows.forEach(row => {
      html += '<tr>';
      row.forEach(cell => { html += '<td>' + (cell ?? '') + '</td>'; });
      html += '</tr>';
    });
    html += '</tbody></table></div>';
    return html;
  }

  function bindTableSearch(searchId) {
    const inp = document.getElementById(searchId);
    const tbl = document.getElementById(searchId + 'Table');
    if (!inp || !tbl) return;
    inp.oninput = () => {
      const q = inp.value.toLowerCase();
      tbl.querySelectorAll('tbody tr').forEach(tr => {
        tr.style.display = tr.textContent.toLowerCase().includes(q) ? '' : 'none';
      });
    };
  }

  function buildSidebar() {
    const groups = [
      { title: 'Overview', ids: ['tracker', 'export'] },
      { title: 'Excel report views', ids: ['dashboard', 'companies', 'it-budgets', 'technology', 'executives', 'outsourcing', 'leads', 'attribution', 'source-summary'] },
      { title: 'Scrapers', ids: ['scraper-activity', 'scraper-config'] }
    ];
    let html = '<div class="sidebar-brand">AG ONE<small>Sentiment Sales · LSE Research</small></div>';
    groups.forEach(g => {
      html += '<div class="nav-section"><div class="nav-section-title">' + esc(g.title) + '</div>';
      g.ids.forEach(id => {
        const v = state.views.find(x => x.viewId === id);
        if (!v) return;
        const active = state.currentView === id ? ' active' : '';
        html += '<a class="nav-item' + active + '" data-view="' + id + '"><span class="icon">' + esc(v.icon) + '</span><span>' + esc(v.title) + '</span></a>';
      });
    });
    $('sidebar').innerHTML = html;
    $('sidebar').querySelectorAll('.nav-item').forEach(el => {
      el.onclick = (e) => { e.preventDefault(); navigate(el.dataset.view); };
    });
  }

  function navigate(viewId) {
    state.currentView = viewId;
    location.hash = viewId;
    const v = state.views.find(x => x.viewId === viewId);
    $('pageTitle').textContent = v ? v.title : 'AG ONE';
    $('pageSubtitle').textContent = v ? (v.excelSheetName !== '—' ? 'Excel: ' + v.excelSheetName : v.description) : '';
    buildSidebar();
    renderView(viewId);
  }

  async function loadViews() {
    state.views = await api('/api/reports/views');
    if (!location.hash || location.hash === '#') location.hash = 'tracker';
    navigate(location.hash.replace('#', '') || 'tracker');
  }

  window.addEventListener('hashchange', () => navigate(location.hash.replace('#', '') || 'tracker'));

  async function renderView(viewId) {
    const root = $('viewRoot');
    root.innerHTML = '<div class="empty-state"><p>Loading view…</p></div>';
    try {
      if (viewId === 'tracker') await renderTracker(root);
      else if (viewId === 'scraper-config') await renderScraperConfig(root);
      else if (viewId === 'scraper-activity') await renderScraperActivity(root);
      else if (viewId === 'export') await renderExport(root);
      else await renderReportTable(root, viewId);
    } catch (e) {
      root.innerHTML = '<div class="card"><div class="card-body empty-state"><h3>Could not load view</h3><p>' + esc(e.message) + '</p><button class="btn btn-primary" onclick="location.reload()">Reload</button></div></div>';
    }
  }

  async function renderReportTable(root, viewId) {
    const data = await api('/api/reports/' + viewId + jobQuery());
    const v = state.views.find(x => x.viewId === viewId);
    let headers = [], rows = [];

    if (Array.isArray(data) && data.length === 0) {
      root.innerHTML = '<div class="card"><div class="card-body empty-state"><h3>No data yet</h3><p>Run research to populate <strong>' + esc(v?.excelSheetName || viewId) + '</strong>.</p><button class="btn btn-primary" id="emptyRun">Run research (20)</button></div></div>';
      $('emptyRun')?.addEventListener('click', () => startResearch(20));
      return;
    }

    if (viewId === 'dashboard') {
      return renderDashboardView(root, data);
    }

    if (Array.isArray(data)) {
      headers = Object.keys(data[0] || {});
      rows = data.map(row => headers.map(h => {
        let val = row[h];
        if (h.toLowerCase().includes('offshoring') && typeof val === 'string') return offshoringPill(val);
        if (val == null) return '';
        if (typeof val === 'number') return Number.isInteger(val) ? val : val.toFixed(2);
        return esc(String(val));
      }));
    }

    root.innerHTML = '<div class="card"><div class="card-header"><h2>' + esc(v?.title || viewId) + '</h2><span style="font-size:12px;color:var(--muted)">' + rows.length + ' rows</span></div><div class="card-body">' +
      renderTable(headers.map(h => h.replace(/([A-Z])/g, ' $1').trim()), rows, 'rpt' + viewId) + '</div></div>';
    bindTableSearch('rpt' + viewId);
  }

  async function renderDashboardView(root, d) {
    const sectors = d.sectors || [];
    root.innerHTML = `
      <div class="kpi-grid">
        <div class="kpi-card"><div class="value">${d.totalCompanies}</div><div class="label">Companies</div></div>
        <div class="kpi-card"><div class="value">${d.confirmedOffshoring}</div><div class="label">Confirmed offshoring</div></div>
        <div class="kpi-card"><div class="value">£${Number(d.totalEstimatedItBudgetGbpB).toFixed(1)}B</div><div class="label">IT budget</div></div>
        <div class="kpi-card"><div class="value">£${Number(d.totalOffshoreSpendGbpB).toFixed(1)}B</div><div class="label">Offshore spend</div></div>
        <div class="kpi-card"><div class="value">${d.companiesWithIndiaOperations}</div><div class="label">India operations</div></div>
      </div>
      <div class="card"><div class="card-header"><h2>Sector breakdown</h2></div><div class="card-body">` +
      renderTable(['Sector', 'Companies', 'IT Budget £B', 'Avg IT %'],
        sectors.map(s => [esc(s.sector), s.companyCount, s.estItBudgetGbpB?.toFixed(2), s.avgItPercentRevenue?.toFixed(1) + '%']), 'sec') +
      '</div></div>';
    bindTableSearch('sec');
  }

  async function renderTracker(root) {
    root.innerHTML = `
      <div class="kpi-grid" id="trackerKpis"></div>
      <div class="card"><div class="card-body">
        <div style="display:flex;justify-content:space-between;margin-bottom:8px"><strong>Research progress</strong><span id="phaseText" style="font-size:12px;color:var(--muted)">—</span></div>
        <div class="progress-track"><div class="progress-fill" id="phaseFill" style="width:0%"></div></div>
      </div></div>
      <div class="chart-row">
        <div class="card chart-card span-8"><div class="card-header"><h2>Live progress</h2></div><div class="card-body chart-box"><canvas id="cProgress"></canvas></div></div>
        <div class="card chart-card span-4"><div class="card-header"><h2>Sources scraped</h2></div><div class="card-body chart-box"><canvas id="cSources"></canvas></div></div>
        <div class="card chart-card span-4"><div class="card-header"><h2>Offshoring</h2></div><div class="card-body chart-box"><canvas id="cOff"></canvas></div></div>
        <div class="card chart-card span-4"><div class="card-header"><h2>Sectors (£B IT)</h2></div><div class="card-body chart-box"><canvas id="cSectors"></canvas></div></div>
        <div class="card chart-card span-4"><div class="card-header"><h2>Top partners</h2></div><div class="card-body chart-box"><canvas id="cPartners"></canvas></div></div>
      </div>`;
    initTrackerCharts();
    await refreshTrackerData();
  }

  function initTrackerCharts() {
    Object.values(state.charts).forEach(c => c.destroy?.());
    state.charts = {};
    const opts = { responsive: true, maintainAspectRatio: false, plugins: { legend: { position: 'bottom' } } };
    state.charts.progress = new Chart($('cProgress'), { type: 'line', data: { labels: [], datasets: [{ label: 'Processed', data: [], borderColor: '#296DF5', fill: true, backgroundColor: 'rgba(41,109,245,0.1)', tension: 0.3 }] }, options: { ...opts, scales: { y: { beginAtZero: true } } } });
    state.charts.sources = new Chart($('cSources'), { type: 'doughnut', data: { labels: [], datasets: [{ data: [], backgroundColor: [] }] }, options: opts });
    state.charts.off = new Chart($('cOff'), { type: 'pie', data: { labels: [], datasets: [{ data: [], backgroundColor: ['#0AA956', '#CC9500', '#D6DBE6'] }] }, options: opts });
    state.charts.sectors = new Chart($('cSectors'), { type: 'bar', data: { labels: [], datasets: [{ data: [], backgroundColor: '#4472C4' }] }, options: { ...opts, indexAxis: 'y', plugins: { legend: { display: false } } } });
    state.charts.partners = new Chart($('cPartners'), { type: 'bar', data: { labels: [], datasets: [{ data: [], backgroundColor: '#7742FF' }] }, options: { ...opts, plugins: { legend: { display: false } } } });
  }

  async function refreshTrackerData() {
    const t = await api('/api/tracker/live' + jobQuery());
    const d = t.dashboard;
    $('trackerKpis').innerHTML = [
      ['Companies', d.totalCompanies], ['Confirmed', d.confirmedOffshoring],
      ['IT £B', d.totalEstimatedItBudgetGbpB?.toFixed(1)], ['Offshore £B', d.totalOffshoreSpendGbpB?.toFixed(1)]
    ].map(([l, v]) => '<div class="kpi-card"><div class="value">' + v + '</div><div class="label">' + l + '</div></div>').join('');

    const off = t.offshoringCounts || {};
    state.charts.off.data.labels = Object.keys(off);
    state.charts.off.data.datasets[0].data = Object.values(off);
    state.charts.off.update();

    const sectors = (d.sectors || []).slice(0, 8);
    state.charts.sectors.data.labels = sectors.map(s => s.sector);
    state.charts.sectors.data.datasets[0].data = sectors.map(s => s.estItBudgetGbpB);
    state.charts.sectors.update();

    const partners = (d.topPartners || []).slice(0, 8);
    state.charts.partners.data.labels = partners.map(p => p.partner);
    state.charts.partners.data.datasets[0].data = partners.map(p => p.companyCount);
    state.charts.partners.update();

    Object.assign(state.sourceCounts, t.sourceCounts || {});
    updateSourceChart();
  }

  function updateSourceChart() {
    if (!state.charts.sources) return;
    const colors = { AnnualReport: '#4472C4', LinkedIn: '#7742FF', JobBoard: '#CC9500', PressRelease: '#0AA956', CompanyWebsite: '#FF6733', InvestorRelations: '#296DF5', Other: '#7288AC' };
    const labels = Object.keys(state.sourceCounts);
    state.charts.sources.data.labels = labels;
    state.charts.sources.data.datasets[0].data = labels.map(k => state.sourceCounts[k]);
    state.charts.sources.data.datasets[0].backgroundColor = labels.map(k => colors[k] || '#296DF5');
    state.charts.sources.update('none');
  }

  function pushProgress(processed, total, phase) {
    const label = new Date().toLocaleTimeString();
    state.progressLabels.push(label);
    state.progressData.push(processed);
    if (state.progressLabels.length > 40) { state.progressLabels.shift(); state.progressData.shift(); }
    if (state.charts.progress) {
      state.charts.progress.data.labels = state.progressLabels;
      state.charts.progress.data.datasets[0].data = state.progressData;
      state.charts.progress.update('none');
    }
    const pct = total > 0 ? Math.round(processed / total * 100) : 0;
    const fill = $('phaseFill');
    const txt = $('phaseText');
    if (fill) fill.style.width = pct + '%';
    if (txt) txt.textContent = (phase || '') + ' — ' + processed + '/' + total + ' (' + pct + '%)';
  }

  async function renderScraperConfig(root) {
    const configs = await api('/api/scraper-config');
    let rows = configs.map(c => [
      esc(c.displayName),
      '<code style="font-size:11px">' + esc(c.sourceType) + '</code>',
      '<span class="pill ' + (c.isEnabled ? 'pill-on' : 'pill-off') + '">' + (c.isEnabled ? 'ON' : 'OFF') + '</span>',
      c.maxItemsToScrape,
      '<span style="font-size:11px;max-width:200px;display:inline-block;overflow:hidden;text-overflow:ellipsis" title="' + esc(c.baseUrlTemplate) + '">' + esc(c.baseUrlTemplate) + '</span>',
      c.delayMsMin + '–' + c.delayMsMax + 'ms',
      c.priority,
      '<button class="btn btn-ghost btn-sm" data-edit="' + c.id + '">Edit</button>'
    ]);

    root.innerHTML = `
      <div class="card"><div class="card-header"><h2>Scraper configuration</h2><button class="btn btn-primary btn-sm" id="btnAddScraper">+ Add source</button></div>
      <div class="card-body">
        <p style="font-size:13px;color:var(--muted);margin:0 0 16px">Configure public data sources: URL template (<code>{ticker}</code>, <code>{company}</code>), max items to scrape per company, and delays.</p>
        ${renderTable(['Name', 'Type', 'Status', 'Max items', 'URL template', 'Delay', 'Priority', ''], rows, 'cfg')}
      </div></div>
      <div class="card" id="configEditor" style="display:none"><div class="card-header"><h2 id="editorTitle">Edit scraper</h2></div><div class="card-body" id="editorBody"></div></div>`;

    bindTableSearch('cfg');
    root.querySelectorAll('[data-edit]').forEach(btn => btn.onclick = () => openEditor(configs.find(c => c.id === +btn.dataset.edit)));
    root.querySelector('#btnAddScraper').onclick = () => openEditor(null);
  }

  function openEditor(cfg) {
    const ed = $('configEditor');
    ed.style.display = '';
    $('editorTitle').textContent = cfg ? 'Edit: ' + cfg.displayName : 'New data source';
    $('editorBody').innerHTML = `
      <div class="config-form-grid">
        <div><label>Source type (unique key)</label><input id="fType" value="${esc(cfg?.sourceType || '')}" ${cfg ? 'readonly' : ''} /></div>
        <div><label>Display name</label><input id="fName" value="${esc(cfg?.displayName || '')}" /></div>
        <div><label>URL template</label><input id="fUrl" value="${esc(cfg?.baseUrlTemplate || 'https://example.com/{ticker}')}" style="grid-column:1/-1" /></div>
        <div><label>Max items to scrape</label><input type="number" id="fMax" value="${cfg?.maxItemsToScrape ?? 10}" min="1" max="100" /></div>
        <div><label>Delay min (ms)</label><input type="number" id="fDmin" value="${cfg?.delayMsMin ?? 80}" /></div>
        <div><label>Delay max (ms)</label><input type="number" id="fDmax" value="${cfg?.delayMsMax ?? 220}" /></div>
        <div><label>Priority</label><input type="number" id="fPri" value="${cfg?.priority ?? 10}" /></div>
        <div><label>Enabled</label><select id="fEn"><option value="true" ${cfg?.isEnabled !== false ? 'selected' : ''}>Yes</option><option value="false" ${cfg?.isEnabled === false ? 'selected' : ''}>No</option></select></div>
        <div style="grid-column:1/-1"><label>Notes</label><textarea id="fNotes" rows="2">${esc(cfg?.notes || '')}</textarea></div>
      </div>
      <div class="form-actions">
        <button class="btn btn-primary" id="fSave">Save</button>
        ${cfg ? '<button class="btn btn-ghost" id="fDel">Delete</button>' : ''}
        <button class="btn btn-secondary" id="fCancel">Cancel</button>
      </div>`;
    $('fSave').onclick = async () => {
      const body = {
        sourceType: $('fType').value.trim(),
        displayName: $('fName').value.trim(),
        baseUrlTemplate: $('fUrl').value.trim(),
        maxItemsToScrape: +$('fMax').value,
        delayMsMin: +$('fDmin').value,
        delayMsMax: +$('fDmax').value,
        priority: +$('fPri').value,
        isEnabled: $('fEn').value === 'true',
        notes: $('fNotes').value || null
      };
      try {
        if (cfg) await api('/api/scraper-config/' + cfg.id, { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) });
        else await api('/api/scraper-config', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) });
        toast('Scraper saved');
        renderView('scraper-config');
      } catch (e) { toast('Save failed: ' + e.message); }
    };
    $('fCancel').onclick = () => { ed.style.display = 'none'; };
    $('fDel')?.addEventListener('click', async () => {
      if (!confirm('Delete this scraper configuration?')) return;
      await api('/api/scraper-config/' + cfg.id, { method: 'DELETE' });
      toast('Deleted');
      renderView('scraper-config');
    });
  }

  async function renderScraperActivity(root) {
    const data = await api('/api/reports/scraper-activity' + jobQuery());
    const timeline = data.timeline || [];
    const recent = data.recent || [];

    let feed = recent.map(e => `<div class="scrape-item" style="border-left-color:${sourceColor(e.sourceType)}">
      <time>${new Date(e.extractedAt).toLocaleString()}</time> · <strong>${esc(e.sourceLabel)}</strong> · ${esc(e.companyName)}<br>
      <strong>${esc(e.fieldName)}</strong>: ${esc(e.extractedValue)}<br>
      <a href="${esc(e.sourceUrl)}" target="_blank" rel="noopener">${esc(e.sourceUrl)}</a> · ${(e.confidenceScore * 100).toFixed(0)}%
    </div>`).join('');

    root.innerHTML = `
      <div class="card"><div class="card-header"><h2>Scrape timeline</h2></div><div class="card-body">` +
      (timeline.length ? renderTable(['Source', 'Date', 'Facts extracted'],
        timeline.map(t => [esc(t.sourceType), t.date?.split('T')[0] || t.date, t.count]), 'tl') :
        '<p class="empty-state">No scrape events yet.</p>') +
      `</div></div>
      <div class="card"><div class="card-header"><h2>Recent extractions (when & what)</h2></div><div class="card-body scrape-feed">${feed || '<p>No extractions yet.</p>'}</div></div>`;
    bindTableSearch('tl');
  }

  function sourceColor(t) {
    return { AnnualReport: '#4472C4', LinkedIn: '#7742FF', JobBoard: '#CC9500', PressRelease: '#0AA956', CompanyWebsite: '#FF6733' }[t] || '#296DF5';
  }

  async function renderExport(root) {
    const sheets = await api('/api/export/excel/info');
    const latest = await api('/api/tracker/live');
    const job = latest.latestJob;
    root.innerHTML = `
      <div class="card"><div class="card-header"><h2>Export & reports</h2></div><div class="card-body">
        <p style="margin-top:0">Download the full professional Excel workbook (same data as all report views).</p>
        <div style="display:flex;gap:10px;flex-wrap:wrap;margin:16px 0">
          <a class="btn btn-primary" href="/api/export/excel${jobQuery()}">Download Excel workbook</a>
          <button class="btn btn-secondary" id="expRefresh">Refresh status</button>
        </div>
        ${job ? '<p><strong>Last job:</strong> ' + esc(job.status) + ' · ' + job.processedCount + '/' + job.targetCount +
          (job.outputFilePath ? '<br><code>' + esc(job.outputFilePath) + '</code>' : '') + '</p>' : '<p>No research jobs yet.</p>'}
        <h3 style="font-size:14px;margin:24px 0 12px">Workbook sheets (${sheets.sheets?.length || 9})</h3>
        <div style="display:flex;flex-wrap:wrap;gap:8px">${(sheets.sheets || []).map(s => '<span class="pill" style="background:#4472C4;color:#fff">' + esc(s) + '</span>').join('')}</div>
      </div></div>`;
    $('expRefresh')?.addEventListener('click', () => renderExport(root));
  }

  async function connectSignalR(id) {
    try {
      if (state.progressConn) await state.progressConn.stop();
      if (state.extractConn) await state.extractConn.stop();
    } catch (_) {}
    state.progressConn = new signalR.HubConnectionBuilder().withUrl('/hubs/research-progress').withAutomaticReconnect().build();
    state.extractConn = new signalR.HubConnectionBuilder().withUrl('/hubs/extraction').withAutomaticReconnect().build();
    state.progressConn.on('ResearchProgress', p => {
      if (String(p.jobId).toLowerCase() !== String(id).toLowerCase()) return;
      pushProgress(p.processed, p.total, p.phase);
      if (p.phase === 'Completed') { toast('Research complete — Excel ready'); refreshTrackerData(); stopPoll(); loadJobs(); }
      if (p.phase === 'Failed') { toast('Research failed: ' + (p.message || '')); stopPoll(); }
    });
    state.extractConn.on('ExtractionReceived', e => {
      state.sourceCounts[e.sourceType] = (state.sourceCounts[e.sourceType] || 0) + 1;
      updateSourceChart();
    });
    await state.progressConn.start();
    await state.extractConn.start();
    await state.progressConn.invoke('JoinJob', id);
    await state.extractConn.invoke('JoinJob', id);
  }

  function startPoll(id) {
    stopPoll();
    state.pollTimer = setInterval(async () => {
      try {
        const j = await api('/api/research/jobs/' + id);
        pushProgress(j.processedCount, j.targetCount, j.status);
        if (j.status === 'Completed' || j.status === 'Failed') { stopPoll(); loadJobs(); if (state.currentView === 'tracker') refreshTrackerData(); }
      } catch (_) {}
    }, 2500);
  }

  function stopPoll() { if (state.pollTimer) { clearInterval(state.pollTimer); state.pollTimer = null; } }

  async function startResearch(n) {
    toast('Starting research…');
    state.progressLabels = [];
    state.progressData = [];
    Object.keys(state.sourceCounts).forEach(k => delete state.sourceCounts[k]);
    const j = await api('/api/research/start', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ companyCount: n }) });
    state.jobId = j.jobId;
    $('jobSelect').value = j.jobId;
    $('btnExcel').href = '/api/export/excel?jobId=' + j.jobId;
    await connectSignalR(j.jobId);
    startPoll(j.jobId);
    navigate('tracker');
  }

  async function loadJobs() {
    const sel = $('jobSelect');
    const cur = sel.value;
    try {
      const latest = await api('/api/tracker/live');
      const opts = '<option value="">All jobs / latest</option>';
      if (latest.latestJob) {
        const j = latest.latestJob;
        sel.innerHTML = opts + '<option value="' + j.jobId + '">' + j.status + ' · ' + j.processedCount + '/' + j.targetCount + '</option>';
        if (cur) sel.value = cur;
      } else sel.innerHTML = opts;
    } catch (_) {}
  }

  $('jobSelect').onchange = () => {
    state.jobId = $('jobSelect').value || null;
    $('btnExcel').href = '/api/export/excel' + jobQuery();
    if (state.currentView) renderView(state.currentView);
  };

  $('btnRun20').onclick = () => startResearch(20);
  $('btnRun100').onclick = () => startResearch(100);
  $('btnExcel').onclick = async (e) => {
    e.preventDefault();
    try {
      const d = await api('/api/research/dashboard');
      if (!d.totalCompanies) { toast('Run research first'); return; }
      window.location.href = $('btnExcel').href;
    } catch (err) { toast(err.message); }
  };

  loadViews().then(loadJobs).catch(e => {
    $('viewRoot').innerHTML = '<div class="empty-state"><h3>Startup error</h3><p>' + esc(e.message) + '</p><p>Ensure SQL Server is running and API started.</p></div>';
  });
})();
