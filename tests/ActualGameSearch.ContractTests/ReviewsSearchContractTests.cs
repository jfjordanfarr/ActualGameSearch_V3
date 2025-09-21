using System.Net.Http.Json;
using Xunit;

using Microsoft.AspNetCore.Mvc.Testing;
using System.Text.Json;

namespace ActualGameSearch.ContractTests;

public class ReviewsSearchContractTests : IClassFixture<ApiTestFactory>
{
    private readonly HttpClient _client;
    public ReviewsSearchContractTests(ApiTestFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Get_Reviews_Requires_q()
    {
        var resp = await _client.GetAsync("/api/search/reviews");
        Assert.False(resp.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Get_Reviews_Succeeds_With_Result_Wrapping_Items_Array()
    {
        var resp = await _client.GetAsync("/api/search/reviews?q=platformers");
        Assert.True(resp.IsSuccessStatusCode);
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.TryGetProperty("ok", out var ok));
        Assert.Equal(JsonValueKind.True, ok.ValueKind);
        Assert.True(json.TryGetProperty("data", out var data));
        Assert.True(data.TryGetProperty("items", out var items));
        Assert.Equal(JsonValueKind.Array, items.ValueKind);
    }
}
