using System.Text.Json;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ActualGameSearch.IntegrationTests;

public class ConvergenceTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    public ConvergenceTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Reviews_And_Grouped_Accept_Convergence_Params()
    {
        var reviews = await _client.GetAsync("/api/search/reviews?q=test&convergence.minReviewMatches=2&convergence.requireGameAndReview=true");
        var grouped = await _client.GetAsync("/api/search?q=test&convergence.minReviewMatches=1");

        Assert.True(reviews.IsSuccessStatusCode);
        Assert.True(grouped.IsSuccessStatusCode);

        var rJson = await reviews.Content.ReadFromJsonAsync<JsonElement>();
        var gJson = await grouped.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(rJson.TryGetProperty("ok", out var rOk) && rOk.GetBoolean());
        Assert.True(gJson.TryGetProperty("ok", out var gOk) && gOk.GetBoolean());
    }
}
