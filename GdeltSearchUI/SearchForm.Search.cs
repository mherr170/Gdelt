namespace GdeltSearchUI;

public sealed partial class SearchForm
{
    private async Task SearchAsync()
    {
        var query = _queryBox.Text.Trim();
        if (string.IsNullOrEmpty(query)) { SetStatus("Please enter a search query."); return; }

        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        SetBusy(true);
        SetStatus("Searching…");
        _grid.Rows.Clear();

        var result = await TryFetchAsync(() => _client.SearchAsync(
            query,
            Timespans[_timespanBox.SelectedIndex].Hours,
            Modes[_modeBox.SelectedIndex].Value,
            _cts.Token));

        if (result is null) { SetBusy(false); return; }

        if (result.TimedOut)   { SetStatus("Request timed out after 2 attempts."); ErrorDialog.Show(this, "The GDELT API did not respond within 60 seconds after retrying. Try a shorter timespan or a simpler query."); SetBusy(false); return; }
        if (!result.IsSuccess) { SetStatus("Error — see details."); ErrorDialog.Show(this, result.ErrorMessage!); SetBusy(false); return; }

        var articles = ApplyFilter(result.Articles, query);

        if (articles.Count == 0) { SetStatus("No articles found for that query and timespan."); SetBusy(false); return; }

        PopulateGrid(articles);
        FinishSearch(articles.Count, result.FromCache);
        SetBusy(false);
    }

    private async Task LaunchPresetSearchAsync(string query)
    {
        _queryBox.Text = query;
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        var hours = Timespans[_timespanBox.SelectedIndex].Hours;
        var mode  = Modes[_modeBox.SelectedIndex].Value;

        SetBusy(true);
        _grid.Rows.Clear();

        // Phase 1: fast preview
        SetStatus("Loading preview…");
        var preview = await TryFetchAsync(() => _client.SearchAsync(query, hours, mode, token, maxRecords: SearchConstants.PreviewMaxRecords));
        if (preview is null) { SetBusy(false); return; }

        if (preview.IsSuccess && preview.Articles.Count > 0)
        {
            var previewArticles = ApplyFilter(preview.Articles, query);

            if (previewArticles.Count > 0)
            {
                PopulateGrid(previewArticles);
                SetStatus($"{previewArticles.Count} preview articles — loading more…");
            }
        }

        // Phase 2: full results
        if (token.IsCancellationRequested) { SetBusy(false); return; }

        var result = await TryFetchAsync(() => _client.SearchAsync(query, hours, mode, token));
        if (result is null) { SetBusy(false); return; }

        if (result.TimedOut)   { SetStatus("Request timed out after 2 attempts."); ErrorDialog.Show(this, "The GDELT API did not respond. Try a shorter timespan or simpler query."); SetBusy(false); return; }
        if (!result.IsSuccess) { SetStatus("Error — see details."); ErrorDialog.Show(this, result.ErrorMessage!); SetBusy(false); return; }

        var articles = ApplyFilter(result.Articles, query);

        if (articles.Count == 0) { SetStatus("No articles found for that query and timespan."); SetBusy(false); return; }

        PopulateGrid(articles);
        FinishSearch(articles.Count, result.FromCache);
        SetBusy(false);
    }

    private List<GdeltArticle> ApplyFilter(List<GdeltArticle> articles, string query) =>
        _titleOnlyBox.Checked ? ArticleFilter.FilterByTitle(articles, query) : articles;
}
