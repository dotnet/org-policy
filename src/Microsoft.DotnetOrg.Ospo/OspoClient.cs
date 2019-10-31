using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Microsoft.DotnetOrg.Ospo
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

        public async Task<UserLink> GetAsync(string gitHubLogin)
        {
            var result = await GetAsJsonAsync<UserLink>($"people/links/github/{gitHubLogin}");
            if (result != null)
                FixUpEmail(result);

            return result;
        }

        public async Task<IReadOnlyList<UserLink>> GetAllAsync()
        {
            var result = await GetAsJsonAsync<IReadOnlyList<UserLink>>($"people/links");
            foreach (var link in result)
                FixUpEmail(link);

            return result;
        }

        private static void FixUpEmail(UserLink link)
        {
            // For some interesting reason, some people have their
            // email in the PreferredName field...

            var ms = link.MicrosoftInfo;

            if (ms.PreferredName != null && ms.PreferredName.Contains("@"))
            {
                if (string.IsNullOrEmpty(ms.EmailAddress))
                {
                    ms.EmailAddress = ms.PreferredName;
                    ms.PreferredName = null;
                }
            }
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
