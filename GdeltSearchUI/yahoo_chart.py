#!/usr/bin/env python3
"""
yahoo_chart.py  --  Energy futures price history chart.
Usage: python yahoo_chart.py <input_json_file> <output_png_file>

Input JSON (history of post snapshots):
[
  {"ts": "2026-04-28T22:24:14+00:00", "p": {"BRENT_CRUDE": 82.10, "WTI_CRUDE": 78.45, ...}},
  ...
]

Each instrument's values are normalised to 0% at the first snapshot so mixed
price scales (crude ~$80, nat gas ~$2) are comparable on one chart.
If only one snapshot exists, a scatter point is plotted at 0%.
"""
import sys
import json
from datetime import datetime, timezone
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
import matplotlib.dates as mdates
import matplotlib.ticker as mticker


SERIES = [
    ("BRENT_CRUDE",   "Brent Crude",   "#4A9FE0"),
    ("WTI_CRUDE",     "WTI Crude",     "#4FB56E"),
    ("NATURAL_GAS",   "Natural Gas",   "#F0A832"),
    ("RBOB_GASOLINE", "RBOB Gasoline", "#E06060"),
    ("HEATING_OIL",   "Heating Oil",   "#B07AE0"),
]


def parse_ts(s):
    """Parse ISO-8601 timestamp to UTC datetime."""
    try:
        return datetime.fromisoformat(s).astimezone(timezone.utc).replace(tzinfo=None)
    except Exception:
        return None


def main():
    if len(sys.argv) != 3:
        print("Usage: yahoo_chart.py <input.json> <output.png>", file=sys.stderr)
        sys.exit(1)

    with open(sys.argv[1], encoding='utf-8') as f:
        history = json.load(f)

    if not history:
        print("No history data.", file=sys.stderr)
        sys.exit(1)

    # Parse timestamps
    points = []
    for h in history:
        ts = parse_ts(h.get('ts', ''))
        if ts:
            points.append((ts, h.get('p', {})))
    points.sort(key=lambda x: x[0])

    if not points:
        print("No valid timestamps.", file=sys.stderr)
        sys.exit(1)

    fig, ax = plt.subplots(figsize=(9, 4.5))
    fig.patch.set_facecolor('#1e1e22')
    ax.set_facecolor('#28282c')

    plotted = 0
    for code, label, color in SERIES:
        xs, ys = [], []
        baseline = None
        for ts, prices in points:
            if code not in prices:
                continue
            val = prices[code]
            if baseline is None:
                baseline = val
            if baseline == 0:
                continue
            pct = (val - baseline) / baseline * 100.0
            xs.append(ts)
            ys.append(pct)

        if not xs:
            continue

        if len(xs) == 1:
            ax.scatter(xs, ys, color=color, s=60, zorder=4, label=label)
        else:
            ax.plot(xs, ys, color=color, linewidth=2.0,
                    marker='o', markersize=4, label=label, zorder=3)
        plotted += 1

    if plotted == 0:
        print("No plottable series.", file=sys.stderr)
        sys.exit(1)

    # X-axis: smart date formatting based on span
    if len(points) >= 2:
        span = (points[-1][0] - points[0][0]).total_seconds() / 3600
        if span < 24:
            ax.xaxis.set_major_formatter(mdates.DateFormatter('%H:%M'))
            ax.xaxis.set_major_locator(mdates.HourLocator())
        elif span < 24 * 7:
            ax.xaxis.set_major_formatter(mdates.DateFormatter('%b %d\n%H:%M'))
            ax.xaxis.set_major_locator(mdates.DayLocator())
        else:
            ax.xaxis.set_major_formatter(mdates.DateFormatter('%b %d'))
            ax.xaxis.set_major_locator(mdates.WeekdayLocator())
        plt.xticks(rotation=0)

    ax.axhline(0, color='#666', linewidth=0.8, linestyle='--', zorder=2)
    ax.yaxis.set_major_formatter(mticker.FormatStrFormatter('%+.1f%%'))
    ax.set_ylabel('% change from first post', color='#aaa', fontsize=9)
    n = len(points)
    ax.set_title(f'Energy Futures — Post History ({n} snapshot{"s" if n != 1 else ""})',
                 color='#e0e0e0', fontsize=11, fontweight='bold', pad=10)

    ax.legend(loc='upper left', fontsize=8.5,
              facecolor='#2a2a2e', edgecolor='#444', labelcolor='#ddd')
    for spine in ax.spines.values():
        spine.set_color('#444')
    ax.tick_params(axis='both', colors='#999', labelsize=8.5)
    ax.grid(axis='y', color='#383838', linewidth=0.5, zorder=1)
    ax.set_axisbelow(True)

    fig.autofmt_xdate(rotation=0, ha='center')
    plt.tight_layout(pad=1.2)
    plt.savefig(sys.argv[2], dpi=150, bbox_inches='tight',
                facecolor=fig.get_facecolor())
    plt.close()


if __name__ == '__main__':
    main()
