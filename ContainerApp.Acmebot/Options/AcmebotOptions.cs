using System.ComponentModel.DataAnnotations;

namespace ContainerApp.Acmebot.Options
{
    public class AcmebotOptions
    {
        [Required]
        [Url]
        public string Endpoint { get; set; } = "https://acme-v02.api.letsencrypt.org/";

        [Required]
        public string Contacts { get; set; }

        public string Password { get; set; } = "P@ssw0rd";


        [Url]
        public string Webhook { get; set; }

        [Required]
        public string Environment { get; set; } = "AzureCloud";

        public string PreferredChain { get; set; }

        public bool MitigateChainOrder { get; set; } = false;

        [Range(0, 365)]
        public int RenewBeforeExpiry { get; set; } = 30;

        public ExternalAccountBindingOptions ExternalAccountBinding { get; set; }

        // Properties should be in alphabetical order
        public AzureDnsOptions AzureDns { get; set; }

        public CloudflareOptions Cloudflare { get; set; }

        public CustomDnsOptions CustomDns { get; set; }

        public DnsMadeEasyOptions DnsMadeEasy { get; set; }

        public GandiOptions Gandi { get; set; }

        public GoDaddyOptions GoDaddy { get; set; }

        // Backward compatibility, Remove in the future
        public GoogleDnsOptions Google { get; set; }

        public GoogleDnsOptions GoogleDns { get; set; }

        public GratisDnsOptions GratisDns { get; set; }

        public Route53Options Route53 { get; set; }
    }
}
