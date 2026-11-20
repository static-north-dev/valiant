namespace StaticNorth.Valiant.Settings.AspNetCore;

internal static class ValiantSettingsUiPage
{
    public const string Html = """
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>Valiant settings</title>
  <style>
    :root {
      color-scheme: light;
      --accent: #5046e5;
      --accent-text: #ffffff;
      --accent-soft: rgba(80, 70, 229, 0.1);
      --bg: #f6f5fb;
      --header-bg: radial-gradient(900px 300px at 85% -10%, rgba(80, 70, 229, 0.08), transparent 60%), #ffffff;
      --sidebar-bg: #ffffff;
      --card-bg: #ffffff;
      --input-bg: #fafafd;
      --chip-bg: #f0eff8;
      --text: #1a1a2e;
      --text-muted: #6b6b80;
      --text-faint: #a5a4ba;
      --border: #e7e5f0;
      --border-faint: #efeef6;
      --track-bg: #efeef6;
      --shadow: 0 1px 3px rgba(30, 20, 80, 0.05), 0 14px 32px rgba(30, 20, 80, 0.07);
      --string-bg: #e9f6f1;
      --string-fg: #0f7a66;
      --number-bg: #eef1fe;
      --number-fg: #3b4ccb;
      --bool-bg: #fff3de;
      --bool-fg: #b4780a;
      --decimal-bg: #e9f1ff;
      --decimal-fg: #2563eb;
      --object-bg: #f1f0f5;
      --object-fg: #6b6b80;
      font-family: Sora, ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
    }

    :root[data-theme="dark"] {
      color-scheme: dark;
      --accent: #8b7cff;
      --accent-text: #141220;
      --accent-soft: rgba(139, 124, 255, 0.16);
      --bg: #12121b;
      --header-bg: radial-gradient(900px 300px at 85% -10%, rgba(139, 124, 255, 0.14), transparent 60%), #14141f;
      --sidebar-bg: #14141f;
      --card-bg: #181826;
      --input-bg: #1d1d2c;
      --chip-bg: rgba(255, 255, 255, 0.06);
      --text: #f1f0fa;
      --text-muted: #9c9bb4;
      --text-faint: #6e6d85;
      --border: #2b2b3d;
      --border-faint: #22222f;
      --track-bg: #22222f;
      --shadow: 0 1px 2px rgba(0, 0, 0, 0.4), 0 16px 40px rgba(0, 0, 0, 0.5);
      --string-bg: rgba(52, 211, 153, 0.16);
      --string-fg: #5eead4;
      --number-bg: rgba(139, 124, 255, 0.18);
      --number-fg: #b4a7ff;
      --bool-bg: rgba(251, 191, 36, 0.16);
      --bool-fg: #fbbf24;
      --decimal-bg: rgba(96, 165, 250, 0.16);
      --decimal-fg: #93c5fd;
      --object-bg: rgba(255, 255, 255, 0.08);
      --object-fg: #9797ad;
    }

    * {
      box-sizing: border-box;
    }

    ::selection {
      background: rgba(139, 124, 255, 0.35);
    }

    body {
      margin: 0;
      background: var(--bg);
      color: var(--text);
    }

    button,
    input {
      font: inherit;
    }

    .shell {
      background: var(--bg);
      color: var(--text);
      display: flex;
      flex-direction: column;
      min-height: 100vh;
      transition: background 0.3s, color 0.3s;
    }

    .header {
      background: var(--header-bg);
      border-bottom: 1px solid var(--border);
      padding: 40px 56px 26px;
      position: relative;
    }

    .header-inner {
      align-items: flex-start;
      display: flex;
      gap: 24px;
      justify-content: space-between;
    }

    .eyebrow {
      color: var(--accent);
      font-size: 11px;
      font-weight: 700;
      letter-spacing: 0.16em;
      text-transform: uppercase;
    }

    h1 {
      color: var(--text);
      font-size: 46px;
      font-weight: 800;
      letter-spacing: 0;
      line-height: 1.05;
      margin: 10px 0 0;
    }

    .subtitle {
      color: var(--text-muted);
      font-size: 16px;
      line-height: 1.55;
      margin: 10px 0 0;
      max-width: 600px;
    }

    .stat-row,
    .chip-row {
      display: flex;
      flex-wrap: wrap;
      gap: 8px;
      margin-top: 20px;
    }

    .chip {
      align-items: center;
      background: var(--chip-bg);
      border-radius: 999px;
      color: var(--text-muted);
      display: inline-flex;
      font-size: 11px;
      font-weight: 600;
      gap: 6px;
      letter-spacing: 0.03em;
      padding: 6px 13px;
    }

    .theme-toggle {
      background: var(--track-bg);
      border: 1px solid var(--border);
      border-radius: 999px;
      cursor: pointer;
      flex-shrink: 0;
      height: 32px;
      margin-top: 4px;
      padding: 0;
      position: relative;
      width: 58px;
    }

    .theme-knob {
      align-items: center;
      background: var(--accent);
      border-radius: 50%;
      color: var(--accent-text);
      display: flex;
      height: 24px;
      justify-content: center;
      left: 3px;
      position: absolute;
      top: 3px;
      transition: left 0.25s ease;
      width: 24px;
    }

    :root[data-theme="dark"] .theme-knob {
      left: 31px;
    }

    .body {
      align-items: flex-start;
      display: flex;
      flex: 1;
    }

    .sidebar {
      align-self: flex-start;
      background: var(--sidebar-bg);
      border-right: 1px solid var(--border);
      flex-shrink: 0;
      max-height: 100vh;
      overflow-y: auto;
      padding: 24px 18px;
      position: sticky;
      top: 0;
      width: 328px;
    }

    .search-wrap {
      margin-bottom: 18px;
      position: relative;
    }

    .search-icon {
      color: var(--text-faint);
      left: 14px;
      pointer-events: none;
      position: absolute;
      top: 50%;
      transform: translateY(-50%);
    }

    .search {
      background: var(--input-bg);
      border: 1.5px solid var(--border);
      border-radius: 999px;
      color: var(--text);
      font-size: 14px;
      outline: none;
      padding: 10px 14px 10px 38px;
      transition: border-color 0.15s;
      width: 100%;
    }

    .search:focus {
      border-color: var(--accent);
    }

    .schema-list {
      display: grid;
      gap: 8px;
    }

    .schema-button {
      align-items: flex-start;
      background: transparent;
      border: 0;
      border-radius: 16px;
      color: var(--text);
      cursor: pointer;
      display: flex;
      gap: 12px;
      padding: 14px;
      text-align: left;
      transition: background 0.15s;
      width: 100%;
    }

    .schema-button:hover {
      background: var(--chip-bg);
    }

    .schema-button[aria-pressed="true"] {
      background: var(--accent);
      color: var(--accent-text);
    }

    .schema-icon {
      align-items: center;
      background: var(--accent-soft);
      border-radius: 12px;
      color: var(--accent);
      display: flex;
      flex-shrink: 0;
      height: 36px;
      justify-content: center;
      width: 36px;
    }

    .schema-button[aria-pressed="true"] .schema-icon {
      background: rgba(255, 255, 255, 0.2);
      color: var(--accent-text);
    }

    .schema-row-main {
      flex: 1;
      min-width: 0;
    }

    .schema-row-title {
      align-items: center;
      display: flex;
      gap: 8px;
      justify-content: space-between;
    }

    .schema-name {
      color: inherit;
      font-size: 14.5px;
      font-weight: 700;
      min-width: 0;
      overflow-wrap: anywhere;
    }

    .schema-count {
      background: var(--chip-bg);
      border-radius: 999px;
      color: var(--text-muted);
      flex-shrink: 0;
      font-size: 10px;
      font-weight: 700;
      letter-spacing: 0.02em;
      padding: 2px 8px;
    }

    .schema-button[aria-pressed="true"] .schema-count {
      background: rgba(255, 255, 255, 0.22);
      color: var(--accent-text);
    }

    .schema-section {
      color: var(--text-muted);
      font-family: "JetBrains Mono", "SFMono-Regular", Consolas, "Liberation Mono", monospace;
      font-size: 11.5px;
      margin-top: 3px;
      opacity: 0.85;
      overflow-wrap: anywhere;
    }

    .schema-button[aria-pressed="true"] .schema-section {
      color: var(--accent-text);
    }

    .empty {
      color: var(--text-muted);
      font-size: 13px;
      line-height: 1.5;
      padding: 24px 8px;
      text-align: center;
    }

    .detail {
      flex: 1;
      min-width: 0;
      padding: 40px 56px 64px;
    }

    .detail-top {
      align-items: flex-start;
      display: flex;
      flex-wrap: wrap;
      gap: 24px;
      justify-content: space-between;
    }

    .detail-title {
      align-items: flex-start;
      display: flex;
      gap: 18px;
    }

    .detail-icon {
      align-items: center;
      background: var(--accent);
      border-radius: 16px;
      color: var(--accent-text);
      display: flex;
      flex-shrink: 0;
      height: 54px;
      justify-content: center;
      width: 54px;
    }

    h2 {
      color: var(--text);
      font-size: 29px;
      font-weight: 800;
      letter-spacing: 0;
      line-height: 1.2;
      margin: 0;
      overflow-wrap: anywhere;
    }

    .description {
      color: var(--text-muted);
      font-size: 15px;
      line-height: 1.55;
      margin: 8px 0 0;
      max-width: 580px;
    }

    .detail-stats {
      display: flex;
      flex-shrink: 0;
      flex-wrap: wrap;
      gap: 8px;
    }

    .chip.accent {
      background: var(--accent-soft);
      color: var(--accent);
    }

    .meta-row {
      display: flex;
      flex-wrap: wrap;
      gap: 10px;
      margin-top: 22px;
    }

    .meta-chip,
    .copy-chip {
      align-items: center;
      border-radius: 999px;
      display: inline-flex;
      gap: 8px;
      min-width: 0;
      padding: 7px 14px;
    }

    .meta-chip {
      background: var(--chip-bg);
      border: 1px solid var(--border);
      color: var(--text-muted);
    }

    .copy-chip {
      background: var(--accent-soft);
      border: 1px solid transparent;
      color: var(--accent);
      cursor: pointer;
    }

    .json-view-control {
      background: var(--accent-soft);
      border: 1px solid transparent;
      color: var(--accent);
      cursor: pointer;
    }

    .json-view-control select {
      background: transparent;
      border: 0;
      color: inherit;
      cursor: pointer;
      font: inherit;
    }

    .meta-label {
      color: var(--text-faint);
      font-size: 10px;
      font-weight: 700;
      letter-spacing: 0.08em;
      text-transform: uppercase;
      white-space: nowrap;
    }

    .mono {
      font-family: "JetBrains Mono", "SFMono-Regular", Consolas, "Liberation Mono", monospace;
      font-size: 12px;
      overflow-wrap: anywhere;
    }

    .properties-card {
      background: var(--card-bg);
      border: 1px solid var(--border);
      border-radius: 22px;
      box-shadow: var(--shadow);
      margin-top: 26px;
      overflow: hidden;
    }

    .properties-header {
      align-items: center;
      border-bottom: 1px solid var(--border);
      color: var(--text-muted);
      display: flex;
      flex-wrap: wrap;
      font-size: 11px;
      font-weight: 700;
      gap: 14px;
      justify-content: space-between;
      letter-spacing: 0.1em;
      padding: 16px 26px;
      text-transform: uppercase;
    }

    .property-search-wrap {
      align-items: center;
      display: flex;
      min-width: min(100%, 260px);
      position: relative;
    }

    .property-search-icon {
      color: var(--text-faint);
      left: 12px;
      pointer-events: none;
      position: absolute;
      top: 50%;
      transform: translateY(-50%);
    }

    .property-search {
      background: var(--input-bg);
      border: 1.5px solid var(--border);
      border-radius: 999px;
      color: var(--text);
      font-size: 12.5px;
      letter-spacing: 0;
      outline: none;
      padding: 8px 12px 8px 34px;
      text-transform: none;
      width: 100%;
    }

    .property-search:focus {
      border-color: var(--accent);
    }

    .property-row {
      align-items: flex-start;
      border-bottom: 1px solid var(--border-faint);
      display: flex;
      gap: 18px;
      padding: 20px 26px;
    }

    .property-row:last-child {
      border-bottom: 0;
    }

    .property-badge {
      align-items: center;
      border-radius: 14px;
      display: flex;
      flex-shrink: 0;
      font-size: 10.5px;
      font-weight: 700;
      height: 42px;
      justify-content: center;
      letter-spacing: 0.02em;
      width: 42px;
    }

    .kind-string {
      background: var(--string-bg);
      color: var(--string-fg);
    }

    .kind-number {
      background: var(--number-bg);
      color: var(--number-fg);
    }

    .kind-boolean {
      background: var(--bool-bg);
      color: var(--bool-fg);
    }

    .kind-decimal {
      background: var(--decimal-bg);
      color: var(--decimal-fg);
    }

    .kind-object {
      background: var(--object-bg);
      color: var(--object-fg);
    }

    .property-main {
      flex: 1;
      min-width: 0;
    }

    .property-heading {
      align-items: center;
      display: flex;
      flex-wrap: wrap;
      gap: 9px;
    }

    .property-name {
      color: var(--text);
      font-size: 16.5px;
      font-weight: 700;
      overflow-wrap: anywhere;
    }

    .status {
      align-items: center;
      border-radius: 999px;
      display: inline-flex;
      font-size: 10px;
      font-weight: 700;
      letter-spacing: 0.05em;
      padding: 3px 9px;
      text-transform: uppercase;
    }

    .status.required {
      background: var(--accent);
      color: var(--accent-text);
    }

    .status.nullable {
      background: var(--chip-bg);
      color: var(--text-muted);
    }

    .status.secret {
      background: var(--bool-bg);
      color: var(--bool-fg);
    }

    .property-description {
      color: var(--text-muted);
      font-size: 13.5px;
      line-height: 1.5;
      margin: 6px 0 0;
      max-width: 620px;
    }

    .path-row {
      align-items: center;
      display: flex;
      gap: 7px;
      margin-top: 11px;
      min-width: 0;
    }

    .path-code {
      background: var(--accent-soft);
      border-radius: 8px;
      color: var(--accent);
      font-family: "JetBrains Mono", "SFMono-Regular", Consolas, "Liberation Mono", monospace;
      font-size: 12px;
      overflow-wrap: anywhere;
      padding: 4px 9px;
    }

    .icon-button {
      align-items: center;
      background: transparent;
      border: 0;
      border-radius: 999px;
      color: var(--text-faint);
      cursor: pointer;
      display: inline-flex;
      flex-shrink: 0;
      height: 26px;
      justify-content: center;
      width: 26px;
    }

    .property-type {
      align-items: flex-end;
      display: flex;
      flex-direction: column;
      flex-shrink: 0;
      gap: 7px;
      min-width: 96px;
    }

    .type-name {
      color: var(--text-muted);
      font-family: "JetBrains Mono", "SFMono-Regular", Consolas, "Liberation Mono", monospace;
      font-size: 12px;
      overflow-wrap: anywhere;
      text-align: right;
    }

    .type-pill {
      align-items: center;
      border-radius: 999px;
      display: inline-flex;
      font-size: 10.5px;
      font-weight: 700;
      letter-spacing: 0.03em;
      padding: 3px 10px;
    }

    .default-value {
      color: var(--text-faint);
      font-family: "JetBrains Mono", "SFMono-Regular", Consolas, "Liberation Mono", monospace;
      font-size: 11px;
      text-align: right;
    }

    .json-card {
      background: var(--card-bg);
      border: 1px solid var(--border);
      border-radius: 22px;
      box-shadow: var(--shadow);
      margin-top: 18px;
      overflow: hidden;
    }

    .json-header {
      align-items: center;
      border-bottom: 1px solid var(--border);
      display: flex;
      gap: 16px;
      justify-content: space-between;
      padding: 14px 18px 14px 26px;
    }

    .json-actions {
      align-items: center;
      display: flex;
      flex-shrink: 0;
      gap: 10px;
      justify-content: flex-end;
    }

    .json-title {
      color: var(--text-muted);
      font-size: 11px;
      font-weight: 700;
      letter-spacing: 0.1em;
      text-transform: uppercase;
    }

    .json-code {
      color: var(--text);
      font-family: "JetBrains Mono", "SFMono-Regular", Consolas, "Liberation Mono", monospace;
      font-size: 12.5px;
      line-height: 1.65;
      margin: 0;
      overflow-x: auto;
      padding: 20px 26px 24px;
      tab-size: 2;
    }

    @media (max-width: 980px) {
      .header {
        padding: 32px 24px 24px;
      }

      .body {
        display: block;
      }

      .sidebar {
        border-bottom: 1px solid var(--border);
        border-right: 0;
        max-height: none;
        position: static;
        width: 100%;
      }

      .detail {
        padding: 32px 24px 48px;
      }
    }

    @media (max-width: 680px) {
      h1 {
        font-size: 34px;
      }

      .header-inner,
      .detail-top,
      .detail-title,
      .property-row {
        display: block;
      }

      .theme-toggle {
        margin-top: 22px;
      }

      .detail-icon,
      .property-badge {
        margin-bottom: 14px;
      }

      .property-type {
        align-items: flex-start;
        margin-top: 14px;
      }

      .type-name,
      .default-value {
        text-align: left;
      }

      .meta-chip,
      .copy-chip {
        border-radius: 14px;
        width: 100%;
      }
    }
  </style>
</head>
<body>
  <div class="shell">
    <header class="header">
      <div class="header-inner">
        <div>
          <div class="eyebrow">Settings schema</div>
          <h1>Valiant settings</h1>
          <p class="subtitle">Generated settings schemas, grouped by configuration section and model type.</p>
          <div class="stat-row" id="stats">
            <span class="chip">0 sections</span>
            <span class="chip">0 properties</span>
          </div>
        </div>
        <button class="theme-toggle" id="themeToggle" type="button" aria-label="Toggle theme">
          <span class="theme-knob" id="themeIcon"></span>
        </button>
      </div>
    </header>

    <div class="body">
      <aside class="sidebar">
        <div class="search-wrap">
          <span class="search-icon" aria-hidden="true"></span>
          <input class="search" id="search" type="search" placeholder="Find sections or keys" aria-label="Filter settings">
        </div>
        <div class="schema-list" id="schemaList"></div>
      </aside>

      <main class="detail" id="detail">
        <div class="empty">Loading settings schemas.</div>
      </main>
    </div>
  </div>

  <script>
    const state = {
      schemas: [],
      selected: 0,
      filter: "",
      propertyFilter: "",
      copiedKey: null,
      jsonVisible: false,
      jsonMode: "runtime",
      runtime: null,
      runtimeRequest: null,
      showRuntimeValues: false,
      canRevealSecrets: false,
      theme: localStorage.getItem("valiant-settings-theme") || "light"
    };

    const schemaList = document.getElementById("schemaList");
    const detail = document.getElementById("detail");
    const search = document.getElementById("search");
    const stats = document.getElementById("stats");
    const themeToggle = document.getElementById("themeToggle");
    const themeIcon = document.getElementById("themeIcon");

    const schemaUrl = new URL(window.location.href);
    schemaUrl.pathname = schemaUrl.pathname.replace(/\/?$/, "/schemas");
    schemaUrl.search = "";
    schemaUrl.hash = "";

    function start() {
      applyTheme();
      renderChrome();

      search.addEventListener("input", () => {
        state.filter = search.value.trim().toLowerCase();
        renderList();
        renderSelected();
        loadSelectedRuntime();
      });

      themeToggle.addEventListener("click", () => {
        state.theme = state.theme === "light" ? "dark" : "light";
        localStorage.setItem("valiant-settings-theme", state.theme);
        applyTheme();
      });

      detail.addEventListener("click", event => {
        const button = event.target.closest("[data-copy]");
        if (button) copyText(button.dataset.copy || "", button.dataset.copyKey || "");
        if (event.target.closest("[data-toggle-secrets]")) {
          const reveal = !state.runtime?.secretsRevealed;
          resetRuntime();
          detail.querySelector("#settingsJson").innerHTML = renderRuntimeValues(state.schemas[state.selected]);
          detail.querySelector("#jsonView")?.focus();
          loadSelectedRuntime(reveal);
        }
      });

      loadSchemas();
    }

    function loadSchemas() {
      const url = new URL(schemaUrl);
      url.searchParams.set("schemaOnly", "true");

      return fetchJson(url)
        .then(document => {
          state.schemas = (document.schemas || []).map(schema => ({
            ...schema,
            flatProperties: flattenProperties(schema)
          }));
          state.showRuntimeValues = Boolean(document.showRuntimeValues);
          state.canRevealSecrets = Boolean(document.canRevealSecrets);
          renderChrome();
          renderList();
          renderSelected();
          loadSelectedRuntime();
        })
        .catch(error => {
          detail.innerHTML = `<div class="empty">${escapeHtml(error.message)}</div>`;
        });
    }

    async function fetchJson(url, signal) {
      const response = await fetch(url, { headers: { accept: "application/json" }, signal });
      if (!response.ok) throw new Error("Schema endpoint returned " + response.status);
      return response.json();
    }

    function resetRuntime() {
      state.runtimeRequest?.abort();
      state.runtimeRequest = null;
      state.runtime = null;
    }

    function loadSelectedRuntime(reveal = false) {
      if (!state.showRuntimeValues || !state.jsonVisible || state.jsonMode !== "runtime" ||
          !schemaList.querySelector("button") || state.runtimeRequest || state.runtime) return;

      const schema = state.schemas[state.selected];
      const url = new URL(schemaUrl);
      url.searchParams.set("schemaType", schema.typeName);
      url.searchParams.set("section", schema.sectionPath || "");
      url.searchParams.set("name", schema.name);
      if (reveal) url.searchParams.set("revealSecrets", "true");

      const controller = new AbortController();
      state.runtimeRequest = controller;
      detail.querySelector("#runtimeStatus").textContent = runtimeStatus();
      const complete = result => {
        if (state.runtimeRequest !== controller) return;
        state.runtimeRequest = null;
        state.runtime = result;
        detail.querySelector("#runtimeStatus").textContent = runtimeStatus();
        detail.querySelector("#settingsJson").innerHTML = renderRuntimeValues(schema);
      };

      fetchJson(url, controller.signal).then(complete, error => complete({ runtimeValuesError: error.message }));
    }

    function applyTheme() {
      document.documentElement.dataset.theme = state.theme;
      themeIcon.innerHTML = state.theme === "dark" ? icons.moon(13) : icons.sun(13);
    }

    function renderChrome() {
      const totalProperties = state.schemas.reduce((sum, schema) => sum + schema.flatProperties.length, 0);
      stats.innerHTML = `
        <span class="chip">${state.schemas.length}&nbsp;${state.schemas.length === 1 ? "section" : "sections"}</span>
        <span class="chip">${totalProperties}&nbsp;${totalProperties === 1 ? "property" : "properties"}</span>
      `;
      document.querySelector(".search-icon").innerHTML = icons.search(15);
    }

    function matchesSection(schema) {
      return schema.name.toLowerCase().includes(state.filter) ||
        (schema.sectionPath || "").toLowerCase().includes(state.filter) ||
        schema.typeName.toLowerCase().includes(state.filter);
    }

    function renderList() {
      const visible = state.filter
        ? state.schemas.filter(schema => matchesSection(schema) ||
            schema.flatProperties.some(property => matchesKey(property, state.filter)))
        : state.schemas;
      schemaList.innerHTML = "";

      if (visible.length === 0) {
        resetRuntime();
        schemaList.innerHTML = state.filter
          ? `<div class="empty" role="status">No sections or keys match "${escapeHtml(search.value)}".</div>`
          : "";
        return;
      }

      if (!visible.includes(state.schemas[state.selected])) {
        resetRuntime();
        state.selected = state.schemas.indexOf(visible[0]);
        state.propertyFilter = "";
      }

      visible.forEach(schema => {
        const index = state.schemas.indexOf(schema);
        const button = document.createElement("button");
        button.className = "schema-button";
        button.type = "button";
        button.setAttribute("aria-pressed", String(index === state.selected));
        button.innerHTML = `
          <span class="schema-icon">${iconForSchema(schema, 17)}</span>
          <span class="schema-row-main">
            <span class="schema-row-title">
              <span class="schema-name">${escapeHtml(schema.name)}</span>
              <span class="schema-count">${schema.properties.length}</span>
            </span>
            <span class="schema-section">${escapeHtml(schema.sectionPath || "No section")}</span>
          </span>
        `;
        button.addEventListener("click", () => {
          if (state.selected !== index) resetRuntime();
          state.selected = index;
          state.propertyFilter = "";
          renderList();
          renderSelected();
          schemaList.querySelector('[aria-pressed="true"]')?.focus();
          loadSelectedRuntime();
        });
        schemaList.appendChild(button);
      });
    }

    function renderSelected() {
      if (state.schemas.length === 0) {
        detail.innerHTML = `<div class="empty">No Valiant settings schemas are registered.</div>`;
        return;
      }

      if (state.filter && !schemaList.querySelector("button")) {
        detail.innerHTML = "";
        return;
      }

      const schema = state.schemas[state.selected] || state.schemas[0];
      const properties = schema.flatProperties;
      const { visibleProperties, requiredCount } = getVisibleProperties(schema);
      const hasRuntimeValues = state.showRuntimeValues;
      const selectedJsonView = getSelectedJsonView(hasRuntimeValues);

      detail.innerHTML = `
        <div class="detail-top">
          <div class="detail-title">
            <div class="detail-icon">${iconForSchema(schema, 24)}</div>
            <div>
              <h2>${escapeHtml(schema.name)}</h2>
              <p class="description">${escapeHtml(descriptionForSchema(schema))}</p>
            </div>
          </div>
          <div class="detail-stats" id="propertyStats">
            ${renderPropertyStats(properties, visibleProperties, requiredCount)}
          </div>
        </div>

        <div class="meta-row">
          <span class="meta-chip">
            <span class="meta-label">Fully qualified name</span>
            <span class="mono">${escapeHtml(schema.typeName)}</span>
          </span>
          <button class="copy-chip" type="button" data-copy="${escapeAttribute(schema.sectionPath || "")}" data-copy-key="section">
            <span class="meta-label">Section path</span>
            <span class="mono">${escapeHtml(schema.sectionPath || "No configuration section")}</span>
            <span class="copy-icon">${copyIcon("section")}</span>
          </button>
          <label class="meta-chip json-view-control">
            <span class="meta-label">JSON view</span>
            <select id="jsonView" class="mono">
              <option value="hidden" ${selectedJsonView === "hidden" ? "selected" : ""}>Hidden</option>
              <option value="schema" ${selectedJsonView === "schema" ? "selected" : ""}>Schema</option>
              ${hasRuntimeValues ? `<option value="runtime" ${selectedJsonView === "runtime" ? "selected" : ""}>Runtime</option>` : ""}
            </select>
          </label>
          ${hasRuntimeValues
            ? `<span class="meta-chip">
                <span class="meta-label">Runtime values</span>
                <span class="mono" id="runtimeStatus" role="status">${runtimeStatus()}</span>
              </span>`
            : ""}
        </div>

        <div id="settingsJson">${renderJsonPanel(schema, hasRuntimeValues)}</div>

        <section class="properties-card">
          <div class="properties-header">
            <span>Properties</span>
            <div class="property-search-wrap">
              <span class="property-search-icon" aria-hidden="true">${icons.search(13)}</span>
              <input class="property-search" id="propertySearch" type="search" value="${escapeAttribute(state.propertyFilter)}" placeholder="Filter properties" aria-label="Filter properties">
            </div>
          </div>
          <div id="propertyResults">${renderPropertyResults(properties, visibleProperties)}</div>
        </section>
      `;

      detail.querySelector("#jsonView")?.addEventListener("change", event => {
        setJsonView(event.currentTarget.value);
        if (!state.jsonVisible || state.jsonMode !== "runtime") resetRuntime();
        renderSelected();
        detail.querySelector("#jsonView")?.focus();
        loadSelectedRuntime();
      });
      detail.querySelector("#propertySearch")?.addEventListener("input", event => {
        state.propertyFilter = event.currentTarget.value;
        const { visibleProperties, requiredCount } = getVisibleProperties(schema);
        detail.querySelector("#propertyStats").innerHTML = renderPropertyStats(properties, visibleProperties, requiredCount);
        detail.querySelector("#propertyResults").innerHTML = renderPropertyResults(properties, visibleProperties);
      });
    }

    function runtimeStatus() {
      if (state.runtimeRequest) return "Loading";
      if (!state.runtime) return "Not loaded";
      return state.runtime.runtimeValuesError ? "Unavailable" : "Options instance";
    }

    function getVisibleProperties(schema) {
      const properties = matchesSection(schema) ? schema.flatProperties
        : schema.flatProperties.filter(property => matchesKey(property, state.filter));
      const visibleProperties = filterProperties(properties);
      return { visibleProperties, requiredCount: visibleProperties.filter(property => property.isRequired).length };
    }

    function renderPropertyStats(properties, visible, requiredCount) {
      return `<span class="chip">${visible.length}${state.propertyFilter ? `/${properties.length}` : ""}&nbsp;${visible.length === 1 ? "property" : "properties"}</span>
              <span class="chip accent">${requiredCount}&nbsp;required</span>`;
    }

    function renderPropertyResults(properties, visible) {
      if (properties.length === 0) return `<div class="empty">This schema has no public settings properties.</div>`;
      if (visible.length === 0) return `<div class="empty">No properties match "${escapeHtml(state.propertyFilter)}".</div>`;
      return visible.map(renderProperty).join("");
    }

    function matchesKey(property, query) {
      return property.name.toLowerCase().includes(query) ||
        property.configurationKey.toLowerCase().includes(query) ||
        property.fullPath.toLowerCase().includes(query);
    }

    function filterProperties(properties) {
      const query = state.propertyFilter.trim().toLowerCase();
      if (!query) return properties;

      return properties.filter(property =>
        matchesKey(property, query) ||
        property.typeName.toLowerCase().includes(query) ||
        String(property.kind || "").toLowerCase().includes(query));
    }

    function getSelectedJsonView(hasRuntimeValues) {
      if (!state.jsonVisible) {
        return "hidden";
      }

      if (state.jsonMode === "runtime" && hasRuntimeValues) {
        return "runtime";
      }

      return "schema";
    }

    function setJsonView(value) {
      if (value === "hidden") {
        state.jsonVisible = false;
        return;
      }

      state.jsonVisible = true;
      state.jsonMode = value === "runtime" ? "runtime" : "schema";
    }

    function renderJsonPanel(schema, hasRuntimeValues) {
      if (!state.jsonVisible) {
        return "";
      }

      if (hasRuntimeValues && state.jsonMode === "runtime") {
        return renderRuntimeValues(schema);
      }

      return renderSectionJson(schema);
    }

    function renderSectionJson(schema) {
      const json = JSON.stringify(buildSectionJson(schema), null, 2);
      return `
        <section class="json-card">
          <div class="json-header">
            <div>
              <div class="json-title">Section JSON</div>
              <div class="mono">${escapeHtml(schema.sectionPath || schema.name)}</div>
            </div>
            <div class="json-actions">
              <button class="copy-chip" type="button" data-copy="${escapeAttribute(json)}" data-copy-key="section-json">
                <span class="meta-label">Copy</span>
                <span class="copy-icon">${copyIcon("section-json")}</span>
              </button>
            </div>
          </div>
          <pre class="json-code"><code>${escapeHtml(json)}</code></pre>
        </section>
      `;
    }

    function renderRuntimeValues(schema) {
      if (!state.runtime) {
        return `<section class="json-card"><div class="empty" role="status">Loading runtime values.</div></section>`;
      }

      if (state.runtime.runtimeValuesError) {
        return `
          <section class="json-card">
            <div class="json-header">
              <div>
                <div class="json-title">Runtime values</div>
                <div class="mono">Unavailable</div>
              </div>
            </div>
            <pre class="json-code"><code>${escapeHtml(state.runtime.runtimeValuesError)}</code></pre>
          </section>
        `;
      }

      const json = JSON.stringify(state.runtime.runtimeValues, null, 2);
      return `
        <section class="json-card">
          <div class="json-header">
            <div>
              <div class="json-title">Runtime values</div>
              <div class="mono">${escapeHtml(schema.sectionPath || schema.name)}</div>
            </div>
            <div class="json-actions">
              <button class="copy-chip" type="button" data-copy="${escapeAttribute(json)}" data-copy-key="runtime-values">
                <span class="meta-label">Copy</span>
                <span class="copy-icon">${copyIcon("runtime-values")}</span>
              </button>
              ${state.canRevealSecrets
                ? `<button class="copy-chip" type="button" data-toggle-secrets>
                    <span class="meta-label">${state.runtime.secretsRevealed ? "Hide secrets" : "Reveal secrets"}</span>
                    <span class="copy-icon">${state.runtime.secretsRevealed ? icons.eyeOff(13) : icons.eye(13)}</span>
                  </button>`
                : ""}
            </div>
          </div>
          <pre class="json-code"><code>${escapeHtml(json)}</code></pre>
        </section>
      `;
    }

    function renderProperty(property) {
      const meta = typeMeta(property.kind, property.typeName);
      const copied = state.copiedKey === property.copyKey;
      const defaultMarkup = property.defaultValue != null
        ? `<span class="default-value">Declared default: ${escapeHtml(property.defaultValue === "" ? '""' : property.defaultValue)}</span>`
        : "";
      const nullableMarkup = property.isNullable
        ? `<span class="status nullable">Nullable</span>`
        : `<span class="status nullable">Non-nullable</span>`;
      const secretMarkup = property.isSecret
        ? `<span class="status secret">Secret</span>`
        : "";

      return `
        <div class="property-row" style="padding-left:${26 + property.depth * 24}px">
          <div class="property-badge ${meta.className}">${meta.abbr}</div>
          <div class="property-main">
            <div class="property-heading">
              <span class="property-name">${escapeHtml(property.name)}</span>
              ${property.isRequired ? `<span class="status required">Required member</span>` : ""}
              ${nullableMarkup}
              ${secretMarkup}
            </div>
            <p class="property-description">${escapeHtml(descriptionForProperty(property))}</p>
            <div class="path-row">
              <code class="path-code">${escapeHtml(property.fullPath)}</code>
              <button class="icon-button" type="button" aria-label="Copy path" data-copy="${escapeAttribute(property.fullPath)}" data-copy-key="${escapeAttribute(property.copyKey)}">
                ${copied ? icons.check(13) : icons.copy(13)}
              </button>
            </div>
          </div>
          <div class="property-type">
            <span class="type-name">${escapeHtml(property.typeName)}</span>
            <span class="type-pill ${meta.className}">${meta.label}</span>
            ${defaultMarkup}
          </div>
        </div>
      `;
    }

    function flattenProperties(schema) {
      const sectionPath = schema.sectionPath || "";
      const output = [];

      const visit = (properties, prefix, depth) => {
        (properties || []).forEach(property => {
          const fullPath = prefix ? `${prefix}:${property.configurationKey}` : property.configurationKey;
          const copyKey = `${schema.typeName}:${fullPath}`;
          output.push({ ...property, depth, fullPath, copyKey });

          if (property.item) {
            visit([{ ...property.item, name: property.item.name === "$item" ? "Item" : property.item.name, configurationKey: "$item" }], fullPath, depth + 1);
          }

          if (property.properties && property.properties.length > 0) {
            visit(property.properties, fullPath, depth + 1);
          }
        });
      };

      visit(schema.properties || [], sectionPath, 0);
      return output;
    }

    function buildSectionJson(schema) {
      return buildObject(schema.properties || []);
    }

    function buildObject(properties) {
      return Object.fromEntries((properties || []).map(property => [property.configurationKey, buildValue(property)]));
    }

    function buildValue(property) {
      if (property.item) {
        return [buildValue(property.item)];
      }

      if (property.properties && property.properties.length > 0) {
        return buildObject(property.properties);
      }

      return sampleValue(property);
    }

    function sampleValue(property) {
      const normalized = String(property.kind || property.typeName || "").toLowerCase();
      if (normalized.includes("bool")) return false;
      if (normalized.includes("int") || normalized.includes("long") || normalized.includes("short")) return 0;
      if (normalized === "number" || normalized.includes("decimal") || normalized.includes("double") || normalized.includes("float")) return 0;
      if (normalized.includes("string")) return "string";
      return null;
    }

    function typeMeta(kind, typeName) {
      const type = String(typeName || "");
      const normalized = String(kind || typeName || "").toLowerCase();
      if (normalized.includes("string")) return { abbr: "Str", label: "Text", className: "kind-string" };
      if (normalized.includes("bool")) return { abbr: "01", label: "Boolean", className: "kind-boolean" };
      if (normalized === "number" || normalized.includes("decimal") || normalized.includes("double") || normalized.includes("float")) return { abbr: "0.0", label: "Number", className: "kind-decimal" };
      if (normalized.includes("int") || normalized.includes("long") || normalized.includes("short")) return { abbr: "123", label: "Number", className: "kind-number" };
      if (normalized.includes("array") || normalized.includes("collection") || type.endsWith("[]")) return { abbr: "[ ]", label: "Array", className: "kind-object" };
      if (normalized === "enum") return { abbr: "Enum", label: "Enum", className: "kind-object" };
      return { abbr: "{ }", label: "Object", className: "kind-object" };
    }

    function descriptionForSchema(schema) {
      if (schema.sectionPath) {
        return `Configuration section: ${schema.sectionPath}`;
      }

      return "Settings model registered without a configuration section path.";
    }

    function descriptionForProperty(property) {
      if (property.depth > 0) {
        return `Nested configuration value mapped from ${property.fullPath}.`;
      }

      return `Configuration value mapped from ${property.fullPath}.`;
    }

    function iconForSchema(schema, size) {
      const value = `${schema.name} ${schema.sectionPath || ""}`.toLowerCase();
      if (value.includes("connection") || value.includes("database") || value.includes("db")) return icons.database(size);
      if (value.includes("search")) return icons.search(size);
      if (value.includes("email") || value.includes("mail") || value.includes("notification")) return icons.mail(size);
      return icons.settings(size);
    }

    function copyText(text, key) {
      if (!text) return;
      if (navigator.clipboard && navigator.clipboard.writeText) {
        navigator.clipboard.writeText(text).catch(() => {});
      }

      state.copiedKey = key;
      renderSelected();
      clearTimeout(copyText.timer);
      copyText.timer = setTimeout(() => {
        state.copiedKey = null;
        renderSelected();
      }, 1400);
    }

    function copyIcon(key) {
      return state.copiedKey === key ? icons.check(13) : icons.copy(13);
    }

    function escapeHtml(value) {
      return String(value)
        .replaceAll("&", "&amp;")
        .replaceAll("<", "&lt;")
        .replaceAll(">", "&gt;")
        .replaceAll("\"", "&quot;")
        .replaceAll("'", "&#039;");
    }

    function escapeAttribute(value) {
      return escapeHtml(value).replaceAll("`", "&#096;");
    }

    const icons = {
      database: size => `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" width="${size}" height="${size}"><ellipse cx="12" cy="5" rx="8" ry="3"></ellipse><path d="M4 5v14c0 1.7 3.6 3 8 3s8-1.3 8-3V5"></path><path d="M4 12c0 1.7 3.6 3 8 3s8-1.3 8-3"></path></svg>`,
      search: size => `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" width="${size}" height="${size}"><circle cx="11" cy="11" r="7"></circle><line x1="21" y1="21" x2="16.65" y2="16.65"></line></svg>`,
      mail: size => `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" width="${size}" height="${size}"><rect x="3" y="5" width="18" height="14" rx="2"></rect><path d="M3 7l9 6 9-6"></path></svg>`,
      settings: size => `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" width="${size}" height="${size}"><path d="M12 15.5A3.5 3.5 0 1 0 12 8a3.5 3.5 0 0 0 0 7.5Z"></path><path d="M19.4 15a1.7 1.7 0 0 0 .3 1.9l.1.1a2 2 0 1 1-2.8 2.8l-.1-.1a1.7 1.7 0 0 0-1.9-.3 1.7 1.7 0 0 0-1 1.6V21a2 2 0 1 1-4 0v-.1a1.7 1.7 0 0 0-1-1.6 1.7 1.7 0 0 0-1.9.3l-.1.1a2 2 0 1 1-2.8-2.8l.1-.1A1.7 1.7 0 0 0 4.6 15a1.7 1.7 0 0 0-1.6-1H3a2 2 0 1 1 0-4h.1a1.7 1.7 0 0 0 1.6-1 1.7 1.7 0 0 0-.3-1.9l-.1-.1a2 2 0 1 1 2.8-2.8l.1.1a1.7 1.7 0 0 0 1.9.3h.1A1.7 1.7 0 0 0 10 3V3a2 2 0 1 1 4 0v.1a1.7 1.7 0 0 0 1 1.6h.1a1.7 1.7 0 0 0 1.9-.3l.1-.1a2 2 0 1 1 2.8 2.8l-.1.1a1.7 1.7 0 0 0-.3 1.9v.1A1.7 1.7 0 0 0 21 10h.1a2 2 0 1 1 0 4H21a1.7 1.7 0 0 0-1.6 1Z"></path></svg>`,
      sun: size => `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" width="${size}" height="${size}"><circle cx="12" cy="12" r="4"></circle><path d="M12 2v2M12 20v2M4.9 4.9l1.4 1.4M17.7 17.7l1.4 1.4M2 12h2M20 12h2M4.9 19.1l1.4-1.4M17.7 6.3l1.4-1.4"></path></svg>`,
      moon: size => `<svg viewBox="0 0 24 24" fill="currentColor" width="${size}" height="${size}"><path d="M12 3a9 9 0 1 0 9 9 7 7 0 0 1-9-9z"></path></svg>`,
      copy: size => `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" width="${size}" height="${size}"><rect x="9" y="9" width="12" height="12" rx="2"></rect><path d="M5 15H4a1 1 0 0 1-1-1V4a1 1 0 0 1 1-1h10a1 1 0 0 1 1 1v1"></path></svg>`,
      check: size => `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round" width="${size}" height="${size}"><polyline points="20 6 9 17 4 12"></polyline></svg>`,
      eye: size => `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" width="${size}" height="${size}"><path d="M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7S2 12 2 12Z"></path><circle cx="12" cy="12" r="3"></circle></svg>`,
      eyeOff: size => `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" width="${size}" height="${size}"><path d="m3 3 18 18"></path><path d="M10.6 10.6A3 3 0 0 0 13.4 13.4"></path><path d="M9.9 4.2A9.6 9.6 0 0 1 12 4c6.5 0 10 8 10 8a17.5 17.5 0 0 1-3.1 4.2"></path><path d="M6.5 6.5C3.6 8.4 2 12 2 12s3.5 8 10 8a9.7 9.7 0 0 0 4.1-.9"></path></svg>`,
      code: size => `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" width="${size}" height="${size}"><polyline points="16 18 22 12 16 6"></polyline><polyline points="8 6 2 12 8 18"></polyline></svg>`
    };

    start();
  </script>
</body>
</html>
""";
}
