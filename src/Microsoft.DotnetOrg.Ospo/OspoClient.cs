using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Microsoft.DotnetOrg.Ospo;

public sealed class OspoClient : IDisposable
{
    private readonly HttpClient _httpClient;

    public OspoClient(string token)
    {
        ArgumentNullException.ThrowIfNull(token);

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://repos.opensource.microsoft.com/api/")
        };
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.DefaultRequestHeaders.Add("api-version", "2019-10-01");
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    public async Task<OspoLink?> GetAsync(string gitHubLogin)
    {
        var result = await GetAsJsonAsync<OspoLink>($"people/links/github/{gitHubLogin}");
        return result;
    }

    public async Task<OspoLinkSet> GetAllAsync()
    {
        var links = await GetAsJsonAsync<IReadOnlyList<OspoLink>>($"people/links");

        var linkSet = new OspoLinkSet
        {
            Links = links ?? Array.Empty<OspoLink>()
        };

        linkSet.Initialize();
        return linkSet;
    }

    private async Task<T?> GetAsJsonAsync<T>(string requestUri)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        var response = await _httpClient.SendAsync(request);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return default;

        if (!response.IsSuccessStatusCode)
        {
            var message = await response.Content.ReadAsStringAsync();

            if (response.StatusCode == HttpStatusCode.Unauthorized)
                throw new OspoUnauthorizedException(message, response.StatusCode);

            throw new OspoException(message, response.StatusCode);
        }

        var responseStream = await response.Content.ReadAsStreamAsync();

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        return await JsonSerializer.DeserializeAsync<T>(responseStream, options);
    }
}