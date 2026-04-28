namespace GdeltSearchUI;

internal static class DarkTheme
{
    // ── Surfaces ──────────────────────────────────────────────────────────────
    internal static readonly Color Background  = Color.FromArgb(0x1E, 0x1E, 0x1E); // main bg
    internal static readonly Color Surface      = Color.FromArgb(0x25, 0x25, 0x26); // panels / toolbar
    internal static readonly Color Raised       = Color.FromArgb(0x2D, 0x2D, 0x30); // column headers / raised controls
    internal static readonly Color Input        = Color.FromArgb(0x3C, 0x3C, 0x3C); // textbox / combobox

    // ── Text ──────────────────────────────────────────────────────────────────
    internal static readonly Color TextPrimary  = Color.FromArgb(0xD4, 0xD4, 0xD4);
    internal static readonly Color TextMuted    = Color.FromArgb(0x85, 0x85, 0x85);

    // ── Accent ────────────────────────────────────────────────────────────────
    internal static readonly Color AccentBlue   = Color.FromArgb(0x00, 0x7A, 0xCC); // search button
    internal static readonly Color PresetBlue   = Color.FromArgb(0x3A, 0x5F, 0xA0); // launcher preset buttons
    internal static readonly Color SelectionBg  = Color.FromArgb(0x09, 0x47, 0x71); // grid row selection
    internal static readonly Color TitleLink    = Color.FromArgb(0x6C, 0xB6, 0xFF); // article title links

    // ── Tone colours (readable on dark bg) ────────────────────────────────────
    internal static readonly Color ToneNeg      = Color.FromArgb(0xF4, 0x87, 0x71); // salmon
    internal static readonly Color TonePos      = Color.FromArgb(0x4E, 0xC9, 0xB0); // teal

    // ── Delta / post-button semantics ─────────────────────────────────────────
    internal static readonly Color DeltaUp          = Color.FromArgb(0xE5, 0x4B, 0x4B); // red  — price rose
    internal static readonly Color DeltaDown        = Color.FromArgb(0x4F, 0xB5, 0x6E); // green — price fell
    internal static readonly Color PostButtonDefault = Color.FromArgb(0x1D, 0x83, 0xBD); // blue  — not yet posted
    internal static readonly Color PostButtonPosted  = Color.FromArgb(0x2E, 0x6E, 0x3E); // green — already posted

    // ── Helpers ───────────────────────────────────────────────────────────────

    internal static void ApplyToContextMenu(ContextMenuStrip menu)
    {
        menu.BackColor = Raised;
        menu.ForeColor = TextPrimary;
        menu.RenderMode = ToolStripRenderMode.System;
    }

    internal static void ApplyToButton(Button btn, Color? back = null)
    {
        btn.BackColor = back ?? Surface;
        btn.ForeColor = TextPrimary;
        btn.FlatStyle = FlatStyle.Flat;
        btn.FlatAppearance.BorderColor = Input;
        btn.FlatAppearance.BorderSize  = 1;
    }
}
