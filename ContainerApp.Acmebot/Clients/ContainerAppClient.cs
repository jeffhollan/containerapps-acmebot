using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Azure.Identity;

namespace ContainerApp.Acmebot
{
    public class ContainerAppClient
    {
        private DefaultAzureCredential _credential;

        public ContainerAppClient(DefaultAzureCredential credential) => _credential = credential;

        public async Task<IList<ManagedEnvironmentCertificate>> GetCertificatesAsync() => throw new NotImplementedException();
        public async Task<ManagedEnvironmentCertificate> GetCertificateAsync(string certificateName) => throw new NotImplementedException();
        public async Task UploadCertificateAsync(string certificateName, byte[] pfxBlob, string password) => throw new NotImplementedException();
    }
}