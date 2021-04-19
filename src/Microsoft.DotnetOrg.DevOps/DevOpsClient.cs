using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Microsoft.DotnetOrg.DevOps
{
    public sealed class DevOpsClient
    {
        private readonly HttpClient _httpClient;

        public DevOpsClient(string organization, string project, string token)
        {
            if (organization is null)
                throw new ArgumentNullException(nameof(organization));

            if (project is null)
                throw new ArgumentNullException(nameof(project));

            if (token is null)
                throw new ArgumentNullException(nameof(token));

            _httpClient = new HttpClient
            {
                BaseAddress = new Uri($@"https://dev.azure.com/{organization}/{project}/_apis/")
            };
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($":{token}")));
            Organization = organization;
            Project = project;
        }

        public string Organization { get; }

        public string Project { get; }

        public Task CheckAsync()
        {
            return GetBuildsAsync(top: 1);
        }

        public async Task<IReadOnlyList<DevOpsBuild>> GetBuildsAsync(string? definitionId = null, string? resultFilter = null, string? reasonFilter = null, int? top = null)
        {
            var uri = $"build/builds?api-version=5.0";

            if (!string.IsNullOrEmpty(definitionId))
                uri += $"&definitions={definitionId}";

            if (!string.IsNullOrEmpty(resultFilter))
                uri += $"&resultFilter={resultFilter}";

            if (!string.IsNullOrEmpty(reasonFilter))
                uri += $"&reasonFilter={reasonFilter}";

            if (top is not null)
                uri += $"&$top={top}";

            var results = await GetAsJsonAsync<Result<IReadOnlyList<DevOpsBuild>>>(uri);
            var builds = results?.Value;
            if (builds is null)
                return Array.Empty<DevOpsBuild>();

            return builds;
        }

        public async Task<DevOpsArtifact?> GetArtifactAsync(int buildId, string artifactName)
        {
            if (artifactName is null)
                throw new ArgumentNullException(nameof(artifactName));

            var uri = $"build/builds/{buildId}/artifacts?artifactName={artifactName}&api-version=5.0";
            var result = await GetAsJsonAsync<DevOpsArtifact>(uri);
            return result;
        }

        public async Task<Stream?> GetArtifactFileAsync(int buildId, string artifactName, string fileName)
        {
            if (artifactName is null)
                throw new ArgumentNullException(nameof(artifactName));

            if (fileName is null)
                throw new ArgumentNullException(nameof(fileName));
            
            var artifact = await GetArtifactAsync(buildId, artifactName);
            if (artifact is null)
                return null;

            var containerId = GetContainerId(artifact);
            var itemPath = WebUtility.UrlEncode(artifact.Name + "/" + fileName);
            var uri = $"https://{Organization}.visualstudio.com/_apis/resources/Containers/{containerId}?itemPath={itemPath}";

            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri(uri)
            };
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));

            var response = await _httpClient.SendAsync(request);

            response.EnsureSuccessStatusCode();

            var compressedStream = await response.Content.ReadAsStreamAsync();
            var stream = new GZipStream(compressedStream, CompressionMode.Decompress);

            return stream;
        }

        private static string? GetContainerId(DevOpsArtifact artifact)
        {
            var dropName = artifact?.Name;
            if (dropName is null)
                return null;

            var data = artifact?.Resource?.Data;

            if (data is null)
                return null;

            if (!data.StartsWith("#/"))
                return null;

            if (!data.EndsWith("/" + dropName))
                return null;

            return data.Substring(2, data.Length - 2 - dropName.Length - 1);
        }

        private async Task<T?> GetAsJsonAsync<T>(string requestUri)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            var response = await _httpClient.SendAsync(request);

            if (response.Content.Headers.ContentType?.MediaType != "application/json")
                throw new DevOpsUnauthorizedException();

            response.EnsureSuccessStatusCode();

            var responseStream = await response.Content.ReadAsStreamAsync();

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            return await JsonSerializer.DeserializeAsync<T>(responseStream, options);
        }
    }
}
