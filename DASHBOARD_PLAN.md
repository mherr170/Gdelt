# Dashboard Redesign Plan

## Goal

Replace the `LauncherForm` button grid with a two-column dashboard:
- **Column 1 (left):** Vertically stacked action buttons (~130 px wide)
- **Column 2 (right):** Per-row stat/description panel that updates dynamically

Add automatic National Debt post logic on startup with a 3-hour idle re-check timer.

---

## Layout Redesign

### Current state
`LauncherForm` uses a `FlowLayoutPanel` with `LeftToRight` wrapping. Buttons are 90×34 px, arranged in a loosely flowing grid. Form is 360×200.

### Target state
Replace `FlowLayoutPanel` with a `TableLayoutPanel`:
- 2 columns: left fixed at ~130 px, right fills remaining width
- N rows: one per widget/action (currently 5: USGunV, US Gas $, Quake, US Debt, Energy $)
- Each row: button on the left, stat label/textbox on the right
- Form resized to ~540×280 (wider to accommodate the stat column)
- `FormBorderStyle` stays `FixedDialog`; height can grow as widgets are added

### Row layout details
Each row in the `TableLayoutPanel` contains:
- **Left cell:** `Button` (Width=120, Height=34, Margin=(0,2,8,2))
- **Right cell:** `Label` (AutoSize=false, Dock=Fill, TextAlign=MiddleLeft, ForeColor=DarkTheme.TextSecondary)

The stat label for each widget shows:
| Widget     | Stat text examples                                          |
|------------|-------------------------------------------------------------|
| USGunV     | "Search: shooting incidents (3 hr window)" *(static)*       |
| US Gas $   | "Last posted: {date} — weekly cadence" / "⚠ Not posted this week" |
| Quake      | "Opens real-time USGS earthquake feed" *(static)*           |
| US Debt    | See "Stat label updates during auto-post" below             |
| Energy $   | "Last post: {date/time}" / "⚠ Not recently posted"          |

Each `Refresh*` method sets both `Text` **and** `ForeColor` per state — green-ish for ✓, amber-ish for ⚠, `DarkTheme.TextSecondary` for neutral — matching the existing button color scheme.

---

## National Debt Auto-Post Logic

### Behavior spec
> **Critical:** `DebtPostTracker.IsTodayPosted()` is a loose 7-day window check. After this redesign it is used **neither** as the auto-post guard **nor** as the button color check — both would diverge from reality. The strict guard `HasBeenPosted(latestRecordDate)` (against the latest Treasury API record date) is the single source of truth. The auto-post flow sets the button color **and** stat label together so they never disagree.

1. **On startup** (`LauncherForm` constructor, after controls are built):
   - Set the debt button to a neutral pending color and the stat label to "Checking…"
   - Fire `SafeAutoPostAsync()` (fire-and-forget with try/catch; never block the UI). It will overwrite both with authoritative state once the API responds.
2. **3-hour idle timer**:
   - A `System.Windows.Forms.Timer` fires every 3 hours (10,800,000 ms)
   - On each tick: call the same `SafeAutoPostAsync()`. The per-record guard prevents re-posting across separate invocations and program restarts; the reentrancy flag in Step 3 prevents overlapping in-flight posts.
   - Timer runs unconditionally on both success and failure paths
   - **Caveat:** `System.Windows.Forms.Timer` does not catch up after machine sleep/hibernate — if the laptop sleeps 6 hours, only one tick fires on resume. The startup re-fire on next app launch is the safety net.

### Layering: helper vs. wrapper
- **`DebtAutoPost.PostIfNeededAsync(CancellationToken ct)`** (in new `DebtAutoPost.cs`) — does the work: fetches, guards on `HasBeenPosted(RecordDate)`, posts if needed, returns a `DebtAutoPostOutcome` enum (`AlreadyPosted` | `Posted` | `MissingCredentials` | `Failed`) with optional error message and the `RecordDate` used. Catches its own exceptions and converts them to `Failed`.
- **`LauncherForm.SafeAutoPostAsync()`** — UI-side wrapper: reentrancy-guards via `_autoPostInFlight`, awaits the helper, switches on the outcome to update both the **button color** (green ✓ for AlreadyPosted/Posted, amber ⚠ for Failed/MissingCredentials) and the **stat label** (text per the table below). Catches anything the helper failed to.

### `DebtAutoPost.PostIfNeededAsync` flow
1. Fetch latest debt figures (`DebtApiClient.GetLatestAsync()`)
2. Read `RecordDate` from the response
3. If `DebtPostTracker.HasBeenPosted(recordDate)` → return `AlreadyPosted` with the recordDate
4. If credentials missing → return `MissingCredentials`
5. Otherwise → run the full post pipeline (sparkline + Bluesky), `MarkPosted(recordDate)`, return `Posted`
6. On any exception → return `Failed` with the error message
7. Honor the `CancellationToken` between each network call

### Stat label & button updates by outcome
| Outcome              | Stat label text                                                            | Button color |
|----------------------|----------------------------------------------------------------------------|--------------|
| (initial / fetching) | "Checking…"                                                                | neutral      |
| AlreadyPosted        | "✓ Posted for {recordDate}"                                                | green        |
| Posted               | "✓ Sent for {recordDate} at {HH:mm}"                                       | green        |
| Failed               | "⚠ Post failed — will retry in 3 hours"                                    | amber        |
| MissingCredentials   | "⚠ Bluesky account not configured — open US Debt and click Post to configure" | amber        |

UI updates are wrapped in `if (!IsDisposed)` checks; the helper's `CancellationToken` comes from a CTS canceled in `FormClosing`. On failure the 3-hour timer continues running unchanged.

---

## Implementation Steps

### Step 1 — Extract debt post logic
`DebtForm` fetches **live data every time** — no cache exists. The full headless flow requires:
1. `DebtApiClient.GetLatestAsync()` — current + previous debt figures
2. `LmStudioPostGenerator.GenerateDebtPostAsync()` — AI headline + hashtags
3. `DebtApiClient.GetRecentAsync(7)` — 7-day history for the sparkline chart
4. `DebtSparkline.RenderPng()` — chart image bytes
5. Post via Bluesky client, then `DebtPostTracker.MarkPosted()`

Extract this into `DebtAutoPost.PostIfNeededAsync(CancellationToken ct)` in a new `DebtAutoPost.cs`, returning a discriminated result like `enum DebtAutoPostOutcome { AlreadyPosted, Posted, MissingCredentials, Failed }` plus an optional error string and the `RecordDate` used.

Key difference from the `DebtForm` path: if credentials are missing, return `MissingCredentials` instead of opening `SettingsDialog`. `DebtForm` continues to handle credential prompting itself in its manual-post flow — no `IWin32Window` parameter needed on the helper.

### Step 2 — Rebuild `LauncherForm` layout
- Replace `FlowLayoutPanel BuildButtonGrid()` with `TableLayoutPanel BuildDashboard()`.
- One row per widget; hold a `Label` field per dynamic-stat widget (`_debtStat`, `_gasStat`, `_energyStat`).
- Static-stat widgets (USGunV, Quake) get one-shot text set at construction; no field needed.
- Update form `Size` to `540×280`.
- In `LauncherForm`'s existing `FormClosed` subscriptions for `DebtForm`/`GasPriceForm`/`CommodityForm` (e.g., `LauncherForm.cs:97`), add a stat-label refresh next to the existing `Refresh*Button()` call. No edits to the form files themselves.
- Note on Debt: `RefreshDebtButton()` currently uses `IsTodayPosted()`. After this redesign, the auto-post flow is the sole writer of debt button color and stat label — `RefreshDebtButton()` is deleted and the existing `Activated`/`FormClosed` subscriptions instead trigger `SafeAutoPostAsync()` (which is cheap when already posted: one API call, then early-returns on `AlreadyPosted`).

### Step 3 — Wire up auto-post on startup
- Fields:
  - `private readonly System.Windows.Forms.Timer _debtTimer;`
  - `private readonly CancellationTokenSource _cts = new();`
  - `private bool _autoPostInFlight;`
- In constructor, after controls built:
  - Subscribe `FormClosing += (_, _) => { _cts.Cancel(); _debtTimer.Stop(); };`
  - Initialize `_debtTimer` with `Interval = 3 * 60 * 60 * 1000`, `Tick += (_, _) => _ = SafeAutoPostAsync();`, then `_debtTimer.Start()`
  - Fire-and-forget the first run: `_ = SafeAutoPostAsync();`
- Override `Dispose(bool disposing)` to dispose `_cts` and `_debtTimer`.
- `SafeAutoPostAsync()` is a wrapper that try/catches every exception and routes failures to the stat label — never let an exception escape (would crash the process for an unobserved task).
- `SafeAutoPostAsync()` is reentrancy-guarded with a simple `bool _autoPostInFlight` field (no Interlocked needed: both startup and Tick run on the UI thread, and the flag is only read/written before any `await`). Set true on entry, false in `finally`; bail out immediately if already set.

### Step 4 — Header update
- Change header text from "Select a search preset" to "GDELT Dashboard".

---

## Files to Change

| File | Change |
|------|--------|
| `LauncherForm.cs` | Rebuild layout, add auto-post logic, timer |
| `DebtForm.Bluesky.cs` | Extract `PostToBlueskyAsync` body into the new helper; `DebtForm` calls it for its manual-post path |
| `DebtAutoPost.cs` | **New file** — static helper for headless debt posting |

Files **not** changing: `CommodityForm.cs`, `SearchForm.cs`, `SettingsDialog.cs`, all trackers.

---

## Resolved Questions

1. **Live data or cache?** Live — `DebtApiClient.GetLatestAsync()` is called on every open. `DebtAutoPost` must replicate the full fetch + sparkline pipeline. See Step 1.
2. **Failure surfacing?** Stat label only. No toast. On failure the 3-hour timer keeps running and retries automatically.
3. **Timer reset at midnight?** No — fires every 3 hours unconditionally. The `HasBeenPosted(RecordDate)` guard handles re-runs naturally: once the Treasury publishes a new RecordDate, the guard returns false and a new post fires.
