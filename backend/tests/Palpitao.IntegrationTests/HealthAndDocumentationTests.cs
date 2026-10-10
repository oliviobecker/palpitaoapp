using System.Net;
using System.Text.Json;

namespace Palpitao.IntegrationTests;

public class HealthAndDocumentationTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Liveness_and_readiness_answer_ok()
    {
        var client = api.CreateClient();

        Assert.Equal("""{"status":"ok","service":"Palpitao.Api"}""", await client.GetStringAsync("/health"));
        Assert.Equal("""{"status":"ok","database":"postgres"}""", await client.GetStringAsync("/health/db"));
    }

    [Fact]
    public async Task The_openapi_document_describes_auth_and_the_group_header()
    {
        var response = await api.CreateClient().GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        Assert.Equal("FanPicks / Palpitão API", document.GetProperty("info").GetProperty("title").GetString());
        var bearer = document.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer");
        Assert.Equal("bearer", bearer.GetProperty("scheme").GetString());

        var listSeasons = document.GetProperty("paths").GetProperty("/seasons").GetProperty("get");
        Assert.Contains(listSeasons.GetProperty("parameters").EnumerateArray(), p => p.GetProperty("name").GetString() == "X-Group-Id");
        Assert.True(listSeasons.GetProperty("responses").TryGetProperty("403", out _));

        var login = document.GetProperty("paths").GetProperty("/Auth/login").GetProperty("post");
        Assert.False(login.TryGetProperty("security", out _)); // anonymous
        Assert.True(login.GetProperty("responses").TryGetProperty("429", out _));
    }

    [Fact]
    public async Task The_scalar_reference_is_served()
    {
        var response = await api.CreateClient().GetAsync("/scalar");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
    }
}
