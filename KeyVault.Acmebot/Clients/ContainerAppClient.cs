using Azure.Identity;

namespace ContainerApp.Acmebot
{
    internal class ContainerAppClient
    {
        private DefaultAzureCredential _credential;

        public ContainerAppClient(DefaultAzureCredential credential) => _credential = credential;
    }
}