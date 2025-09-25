using System.Text.Json;
using ActualGameSearch.Worker.Processing;
using Xunit;

namespace ActualGameSearch.UnitTests;

public class ReviewSanitizerTests
{
    [Fact]
    public void StripPiiFields_PreserveCoreFields()
    {
        var json = """
        {
          "recommendationid": "12345",
          "review": "Great game!",
          "author": {"steamid":"76561198000000000","profile_url":"https://steamcommunity.com/id/some","num_games_owned": 123},
          "username": "someuser",
          "profile": "https://steamcommunity.com/id/someuser",
          "votes_up": 10
        }
        """;
        using var doc = JsonDocument.Parse(json);
        var sanitized = ReviewSanitizer.Sanitize(doc.RootElement);

        // PII removed
        Assert.False(sanitized.TryGetProperty("author", out _));
        Assert.False(sanitized.TryGetProperty("username", out _));
        Assert.False(sanitized.TryGetProperty("profile", out _));
        // Core preserved
        Assert.Equal("12345", sanitized.GetProperty("recommendationid").GetString());
        Assert.Equal("Great game!", sanitized.GetProperty("review").GetString());
        Assert.Equal(10, sanitized.GetProperty("votes_up").GetInt32());
    }

    [Fact]
    public void PreserveReviewHyperlinkField()
    {
        var json = """
        {
          "recommendationid": "abcde",
          "review": "Link should be kept",
          "review_url": "https://store.steampowered.com/recommended/reviews/abcde/",
          "steamid": "76561198000000001"
        }
        """;
        using var doc = JsonDocument.Parse(json);
        var sanitized = ReviewSanitizer.Sanitize(doc.RootElement);

        Assert.Equal("https://store.steampowered.com/recommended/reviews/abcde/", sanitized.GetProperty("review_url").GetString());
        // steamid should be stripped
        Assert.False(sanitized.TryGetProperty("steamid", out _));
    }
}
