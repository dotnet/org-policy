using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Terrajobst.Ospo
{
    public sealed class OspoClient : IDisposable
    {
        private readonly HttpClient _httpClient;

        public OspoClient(string token)
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri("https://repos.opensource.microsoft.com/api/")
            };
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _httpClient.DefaultRequestHeaders.Add("api-version", "2019-10-01");
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($":{token}")));
        }

        public void Dispose()
        {
            _httpClient.Dispose();
        }

        public Task<UserLink> GetAsync(string gitHubLogin)
        {
            return GetAsJsonAsync<UserLink>($"people/links/github/{gitHubLogin}");
        }

        public Task<IReadOnlyList<UserLink>> GetAllAsync()
        {
            return GetAsJsonAsync<IReadOnlyList<UserLink>>($"people/links");
        }

        private async Task<T> GetAsJsonAsync<T>(string requestUri)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            var response = await _httpClient.SendAsync(request);

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
}
