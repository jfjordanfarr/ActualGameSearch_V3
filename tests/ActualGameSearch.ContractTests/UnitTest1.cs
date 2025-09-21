using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace ActualGameSearch.ContractTests;

public class SearchContractTests : IClassFixture<ApiTestFactory>
{
    private readonly HttpClient _client;
    public SearchContractTests(ApiTestFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Get_Grouped_Requires_q()
    {
        var resp = await _client.GetAsync("/api/search");
        Assert.False(resp.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Get_Grouped_Succeeds_With_Result_Wrapping_QueryId_And_Items()
    {
        var resp = await _client.GetAsync("/api/search?q=platformers");
        Assert.True(resp.IsSuccessStatusCode);
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.TryGetProperty("ok", out var ok));
        Assert.Equal(JsonValueKind.True, ok.ValueKind);
        Assert.True(json.TryGetProperty("data", out var data));
        Assert.True(data.TryGetProperty("queryId", out var queryId));
        Assert.Equal(JsonValueKind.String, queryId.ValueKind);
        Assert.True(data.TryGetProperty("items", out var items));
        Assert.Equal(JsonValueKind.Array, items.ValueKind);
    }
}