using System.Text.Json;
using System.Text.Json.Serialization;

namespace GdeltSearchUI;

internal enum BskySortMode { Likes, Reposts, Replies, Quotes }

internal sealed class BskyProfile
{
    [JsonPropertyName("did")]            public string Did            { get; init; } = "";
    [JsonPropertyName("handle")]         public string Handle         { get; init; } = "";
    [JsonPropertyName("displayName")]    public string DisplayName    { get; init; } = "";
    [JsonPropertyName("description")]    public string Description    { get; init; } = "";
    [JsonPropertyName("followersCount")] public int    FollowersCount { get; init; }
    [JsonPropertyName("followsCount")]   public int    FollowsCount   { get; init; }
    [JsonPropertyName("postsCount")]     public int    PostsCount     { get; init; }
    [JsonPropertyName("createdAt")]      public string CreatedAt      { get; init; } = "";
}

internal sealed class BskyAuthor
{
    [JsonPropertyName("did")]            public string Did            { get; init; } = "";
    [JsonPropertyName("handle")]         public string Handle         { get; init; } = "";
    [JsonPropertyName("displayName")]    public string DisplayName    { get; init; } = "";
    [JsonPropertyName("followersCount")] public int    FollowersCount { get; init; }
}

internal sealed class BskyPostRecord
{
    [JsonPropertyName("text")]      public string Text      { get; init; } = "";
    [JsonPropertyName("createdAt")] public string CreatedAt { get; init; } = "";
}

internal sealed class BskyPost
{
    [JsonPropertyName("uri")]         public string         Uri         { get; init; } = "";
    [JsonPropertyName("cid")]         public string         Cid         { get; init; } = "";
    [JsonPropertyName("author")]      public BskyAuthor     Author      { get; init; } = new();
    [JsonPropertyName("record")]      public BskyPostRecord Record      { get; init; } = new();
    [JsonPropertyName("replyCount")]  public int            ReplyCount  { get; init; }
    [JsonPropertyName("repostCount")] public int            RepostCount { get; init; }
    [JsonPropertyName("likeCount")]   public int            LikeCount   { get; init; }
    [JsonPropertyName("quoteCount")]  public int            QuoteCount  { get; init; }
    [JsonPropertyName("indexedAt")]   public string         IndexedAt   { get; init; } = "";

    public string BskyUrl
    {
        get
        {
            // AT URI: at://did:plc:.../app.bsky.feed.post/{rkey}
            var parts = Uri.Split('/');
            var rkey  = parts.Length > 0 ? parts[^1] : "";
            return $"https://bsky.app/profile/{Author.Handle}/post/{rkey}";
        }
    }

    public DateTime CreatedAtLocal =>
        DateTime.TryParse(Record.CreatedAt, null,
            System.Globalization.DateTimeStyles.RoundtripKind, out var dt)
            ? dt.ToLocalTime()
            : DateTime.MinValue;
}

internal sealed class BskyFeedItem
{
    [JsonPropertyName("post")]   public BskyPost     Post   { get; init; } = new();
    [JsonPropertyName("reason")] public JsonElement? Reason { get; init; }
    [JsonPropertyName("reply")]  public JsonElement? Reply  { get; init; }

    public bool IsRepost => Reason.HasValue;
    public bool IsReply  => Reply.HasValue;
}

internal sealed class BskyAuthorFeedResponse
{
    [JsonPropertyName("cursor")] public string?            Cursor { get; init; }
    [JsonPropertyName("feed")]   public List<BskyFeedItem> Feed   { get; init; } = [];
}

internal sealed class BskySearchPostsResponse
{
    [JsonPropertyName("cursor")] public string?        Cursor { get; init; }
    [JsonPropertyName("posts")]  public List<BskyPost> Posts  { get; init; } = [];
}

internal sealed class BskySearchActorsResponse
{
    [JsonPropertyName("cursor")] public string?          Cursor { get; init; }
    [JsonPropertyName("actors")] public List<BskyAuthor> Actors { get; init; } = [];
}

internal sealed class BskyGetProfilesResponse
{
    [JsonPropertyName("profiles")] public List<BskyProfile> Profiles { get; init; } = [];
}

internal sealed class BskySession
{
    [JsonPropertyName("accessJwt")] public string AccessJwt { get; init; } = "";
    [JsonPropertyName("did")]       public string Did       { get; init; } = "";
}
