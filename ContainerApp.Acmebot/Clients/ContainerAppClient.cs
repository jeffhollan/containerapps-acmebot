using System;
using System.Collections.Generic;
using System.Linq;
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

        public async Task<IList<ContainerAppCertificate>> GetCertificatesAsync()
        {
            List<ContainerAppCertificate> certificates = new List<ContainerAppCertificate>();
            var subscriptions = JsonNode.Parse(await _httpClient.GetStringAsync("/subscriptions?api-version=2022-01-01"));
            foreach(var subscription in (JsonArray)subscriptions!["value"])
            {
                var environments = JsonNode.Parse(await _httpClient.GetStringAsync($"{subscription!["id"]}/providers/Microsoft.App/managedEnvironments?api-version=2022-01-01-preview"));
                foreach(var environment in (JsonArray)environments!["value"])
                {
                    certificates.AddRange(await GetCertificatesForEnvironmentAsync((string)environment!["id"]));
                }
            }

            return certificates;
        }
        public async Task<IList<ContainerAppCertificate>> GetCertificatesForContainerAsync(string containerAppId)
        {
            var acaResource = JsonDocument.Parse(await _httpClient.GetStringAsync($"{containerAppId}?api-version=2022-01-01-preview"));
            var environmentId = acaResource.RootElement.GetProperty("properties").GetProperty("managedEnvironmentId").GetString();
            return await GetCertificatesForEnvironmentAsync(environmentId);
        }

        private async Task<IList<ContainerAppCertificate>> GetCertificatesForEnvironmentAsync(string environmentId)
        {
            List<ContainerAppCertificate> certificates = new List<ContainerAppCertificate>();
            var armCerts = JsonNode.Parse(await _httpClient.GetStringAsync($"{environmentId}/certificates?api-version=2022-01-01-preview"));
            foreach (var armCert in (JsonArray)armCerts!["value"])
            {
                var armSubjectName = (string)armCert!["properties"]["subjectName"];
                certificates.Add(new ContainerAppCertificate
                {
                    certificateName = (string)armCert!["name"],
                    subjectName = armSubjectName,
                    dnsNames = armSubjectName.Replace("CN=", ""),
                    issuer = (string)armCert!["properties"]["issueDate"],
                    expirationDate = DateTime.Parse((string)armCert!["properties"]["expirationDate"]),
                    provisioningState = (string)armCert!["properties"]["provisioningState"],
                    environmentId = environmentId
                });
            }
            return certificates;
        }

        public async Task<ContainerAppCertificate> GetCertificateAsync(string certificateName)
        {
            var certificates = await GetCertificatesAsync();
            return certificates.First(x => x.certificateName.Equals(certificateName, StringComparison.OrdinalIgnoreCase));

        }

        public async Task<string> UploadCertificateAsync(CertificatePolicyItem certificatePolicy, byte[] pfxBlob, string password)
        {
            string environmentId;
            if(certificatePolicy.EnvironmentId == null)
            { 
                var acaResource = JsonDocument.Parse(await _httpClient.GetStringAsync($"{certificatePolicy.ContainerAppId}?api-version=2022-01-01-preview"));
                environmentId = acaResource.RootElement.GetProperty("properties").GetProperty("managedEnvironmentId").GetString();
            }
            else
            {
                environmentId = certificatePolicy.EnvironmentId;
            }

            var response = await _httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Put, $"{environmentId}/certificates/{certificatePolicy.CertificateName}?api-version=2022-01-01-preview")
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
            var response = await _httpClient.PostAsync(containerAppId + $"/listCustomHostNameAnalysis?customHostName={dnsName}&api-version=2022-01-01-preview", null);
            var responseContent = await response.Content.ReadAsStringAsync();
        }
        internal async Task BindDomainAsync(string containerAppId, string dnsName, string certificateName)
        {
            var acaResource = JsonNode.Parse(await _httpClient.GetStringAsync($"{containerAppId}?api-version=2022-01-01-preview"));
            var environmentId = (string)acaResource!["properties"]!["managedEnvironmentId"];

            acaResource!["properties"]!["configuration"]!["ingress"]!["customDomains"] = new JsonArray() {
                new JsonObject() {
                    { "name", dnsName },
                    { "certificateId", $"{environmentId}/certificates/{certificateName}" },
                    { "bindingType", "SniEnabled"}
                }
            };

            string stringContent = JsonSerializer.Serialize(acaResource);

            var response = await _httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Put, $"{containerAppId}?api-version=2022-01-01-preview")
            {
                Content = new StringContent(stringContent, System.Text.Encoding.UTF8, "application/json")
            });

            var responseContent = await response.Content.ReadAsStringAsync();
        }

        internal async Task<IList<JsonObject>> GetAppsAsync()
        {
            List<JsonObject> apps = new List<JsonObject>();
            var subscriptions = JsonNode.Parse(await _httpClient.GetStringAsync("/subscriptions?api-version=2022-01-01"));
            foreach (var subscription in (JsonArray)subscriptions!["value"])
            {
                var armApps = JsonNode.Parse(await _httpClient.GetStringAsync($"{subscription!["id"]}/providers/Microsoft.App/containerApps?api-version=2022-01-01-preview"));
                foreach (var app in (JsonArray)armApps!["value"])
                {
                    apps.Add((JsonObject)app);
                }
            }
            return apps;
        }
    }
}
