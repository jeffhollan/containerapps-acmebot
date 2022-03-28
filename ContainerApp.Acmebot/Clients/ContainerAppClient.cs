using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

using Azure.Core;
using Azure.Identity;

using ContainerApp.Acmebot.Models;

namespace ContainerApp.Acmebot
{
    public class ContainerAppClient
    {
        private DefaultAzureCredential _credential;

        public ContainerAppClient(DefaultAzureCredential credential) => _credential = credential;

        public async Task<IList<ManagedEnvironmentCertificate>> GetCertificatesAsync() => throw new NotImplementedException();
        public async Task<ManagedEnvironmentCertificate> GetCertificateAsync(string certificateName) => throw new NotImplementedException();
        public async Task<string> UploadCertificateAsync(CertificatePolicyItem certificatePolicy, byte[] pfxBlob, string password)
        {
            var token = await _credential.GetTokenAsync(new TokenRequestContext(new[] { "https://management.azure.com//.default" }));
            var httpClient = new HttpClient
            {
                BaseAddress = new Uri("https://management.azure.com/"),
                DefaultRequestHeaders =
                {
                    Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.Token)
                }
            };
            var response = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Put, $"/subscriptions/411a9cd0-f057-4ae5-8def-cc1ea96a3933/resourceGroups/d-certtest/providers/Microsoft.App/managedEnvironments/certtest/certificates/{certificatePolicy.CertificateName}?api-version=2022-01-01-preview")
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
    }
}