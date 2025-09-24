using System.Text.Json;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ActualGameSearch.IntegrationTests;

public class CheapPreviewFlowTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    public CheapPreviewFlowTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Games_And_Reviews_Preview_Return_Ok_With_Items()
    {
        var gamesTask = _client.GetAsync("/api/search/games?q=test");
        var reviewsTask = _client.GetAsync("/api/search/reviews?q=test");
        await Task.WhenAll(gamesTask, reviewsTask);

        var gamesResp = await gamesTask;
        var reviewsResp = await reviewsTask;

        Assert.True(gamesResp.IsSuccessStatusCode);
        Assert.True(reviewsResp.IsSuccessStatusCode);

        var gamesJson = await gamesResp.Content.ReadFromJsonAsync<JsonElement>();
        var reviewsJson = await reviewsResp.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(gamesJson.TryGetProperty("ok", out var gamesOk) && gamesOk.GetBoolean());
        Assert.True(reviewsJson.TryGetProperty("ok", out var reviewsOk) && reviewsOk.GetBoolean());

        Assert.True(gamesJson.TryGetProperty("data", out var gamesData));
        Assert.True(reviewsJson.TryGetProperty("data", out var reviewsData));

        Assert.True(gamesData.TryGetProperty("items", out var gamesItems));
        Assert.True(reviewsData.TryGetProperty("items", out var reviewsItems));
        Assert.Equal(JsonValueKind.Array, gamesItems.ValueKind);
        Assert.Equal(JsonValueKind.Array, reviewsItems.ValueKind);
    }
}
