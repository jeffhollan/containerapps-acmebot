using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

using Azure.Core;
using Azure.Identity;

using ContainerApp.Acmebot.Models;

namespace ContainerApp.Acmebot
{
    public class ContainerAppClient
    {
        private DefaultAzureCredential _credential;
        private HttpClient _httpClient;
        public ContainerAppClient(DefaultAzureCredential credential)
        {
            _credential = credential;
            var token = _credential.GetToken(new TokenRequestContext(new[] { "https://management.azure.com//.default" }));
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri("https://management.azure.com/"),
                DefaultRequestHeaders =
                {
                    Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.Token)
                }
            };
        }

        public async Task<IList<ManagedEnvironmentCertificate>> GetCertificatesAsync() => throw new NotImplementedException();
        public async Task<ManagedEnvironmentCertificate> GetCertificateAsync(string certificateName) => throw new NotImplementedException();
        public async Task<string> UploadCertificateAsync(CertificatePolicyItem certificatePolicy, byte[] pfxBlob, string password)
        {
            var acaResource = JsonDocument.Parse(await _httpClient.GetStringAsync($"{certificatePolicy.ContainerAppId}?api-version=2022-01-01-preview"));

            var response = await _httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Put, $"{acaResource.RootElement.GetProperty("properties").GetProperty("managedEnvironmentId").GetString()}/certificates/{certificatePolicy.CertificateName}?api-version=2022-01-01-preview")
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    location = "Central US EUAP",
                    properties = new
                    {
                        password = "P@ssw0rd",
                        value = pfxBlob
                    }
                }), System.Text.Encoding.UTF8, "application/json")
            });

            return await response.Content.ReadAsStringAsync();

        }

        internal async Task<string> GetDomainVerificationIdAsync(string containerAppId)
        {
            var acaResource = JsonDocument.Parse(await _httpClient.GetStringAsync($"{containerAppId}?api-version=2022-01-01-preview"));

            return acaResource.RootElement.GetProperty("properties").GetProperty("customDomainVerificationId").GetString();
        }
        internal async Task ValidateDomainAsync(string containerAppId, string dnsName)
        {
            await _httpClient.PostAsync(containerAppId + $"/listCustomHostNameAnalysis?customHostName={dnsName}&api-version=2022-01-01-preview", null);
        }
        internal async Task BindDomainAsync(string containerAppId, string dnsName, string certificateName)
        {
            var acaResource = JsonNode.Parse(await _httpClient.GetStringAsync($"{containerAppId}?api-version=2022-01-01-preview"));
            var environmentId = (string)acaResource!["properties"]!["managedEnvironmentId"];

            acaResource!["properties"]!["configuration"]!["customDomains"] = new JsonArray() {
                new JsonObject() {
                    { "customDomainName", dnsName },
                    { "certificateName", $"{environmentId}/certificates/{certificateName}" },
                    { "bindingType", "SniEnabled"}
                }
            };

            await _httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Put, $"{containerAppId}?api-version=2022-01-01-preview")
            {
                Content = new StringContent(JsonSerializer.Serialize(acaResource), System.Text.Encoding.UTF8, "application/json")
            });
        }
    }
}