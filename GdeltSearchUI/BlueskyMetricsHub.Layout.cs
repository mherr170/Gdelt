namespace GdeltSearchUI;

internal sealed partial class BlueskyMetricsHub
{
    private record TileDef(string Emoji, string Title, string Sub, Action OnClick);

    private void BuildLayout()
    {
        _statusLabel = new Label
        {
            Dock      = DockStyle.Bottom,
            Height    = 24,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding   = new Padding(8, 0, 0, 0),
            ForeColor = DarkTheme.TextMuted,
            BackColor = DarkTheme.Surface,
            Font      = new Font("Segoe UI", 8.5f),
            Text      = "Select an analysis to run.",
        };

        Controls.Add(BuildTileGrid());
        Controls.Add(BuildHeader());
        Controls.Add(_statusLabel);
    }

    private static Label BuildHeader() => new()
    {
        Text      = "Bluesky Analytics",
        Dock      = DockStyle.Top,
        Height    = 44,
        TextAlign = ContentAlignment.MiddleCenter,
        Font      = new Font("Segoe UI", 10f, FontStyle.Bold),
        ForeColor = DarkTheme.TextPrimary,
        BackColor = DarkTheme.Surface,
    };

    private FlowLayoutPanel BuildTileGrid()
    {
        var flow = new FlowLayoutPanel
        {
            Dock          = DockStyle.Fill,
            Padding       = new Padding(14, 10, 14, 10),
            BackColor     = DarkTheme.Background,
            AutoScroll    = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents  = true,
        };

        TileDef[] tiles =
        [
            new("❤",  "Top by Likes",      "Most-liked posts",         () => OpenTopPosts(BskySortMode.Likes)),
            new("🔁", "Top by Reposts",     "Most-reposted posts",      () => OpenTopPosts(BskySortMode.Reposts)),
            new("💬", "Top by Replies",     "Most-replied posts",       () => OpenTopPosts(BskySortMode.Replies)),
            new("💎", "Top by Quotes",      "Most quote-posted",        () => OpenTopPosts(BskySortMode.Quotes)),
            new("👤", "Account Profile",    "Followers & stats",        () => new BlueskyProfileForm().Show(this)),
            new("🔍", "Topic Search",       "Search all of Bluesky",    () => new BlueskyTopicSearchForm().Show(this)),
            new("👥", "Most Followed",      "Top accounts by topic",    () => new BlueskyMostFollowedForm().Show(this)),
            new("🐦", "Migrations",         "Twitter → Bluesky posts",  () => new BlueskyMigrationsForm().Show(this)),
            new("📊", "Buzz Compare",       "Compare keyword volume",   () => new BlueskyBuzzCompareForm().Show(this)),
            new("🕐", "Best Hour to Post",  "Peak engagement by hour",  () => new BlueskyBestHourForm().Show(this)),
        ];

        foreach (var tile in tiles)
            flow.Controls.Add(MakeTile(tile));

        return flow;
    }

    private static Button MakeTile(TileDef tile)
    {
        var btn = new Button
        {
            Width     = 162,
            Height    = 112,
            Margin    = new Padding(6),
            BackColor = DarkTheme.Raised,
            ForeColor = DarkTheme.TextPrimary,
            FlatStyle = FlatStyle.Flat,
            Cursor    = Cursors.Hand,
            Text      = $"{tile.Emoji}\n{tile.Title}\n{tile.Sub}",
            TextAlign = ContentAlignment.MiddleCenter,
            Font      = new Font("Segoe UI", 9f),
            UseVisualStyleBackColor = false,
        };
        btn.FlatAppearance.BorderColor        = DarkTheme.Input;
        btn.FlatAppearance.BorderSize         = 1;
        btn.FlatAppearance.MouseOverBackColor = DarkTheme.Input;
        btn.Click += (_, _) => tile.OnClick();
        return btn;
    }
}
