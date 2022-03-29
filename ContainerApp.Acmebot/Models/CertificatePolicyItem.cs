using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

using Newtonsoft.Json;

namespace ContainerApp.Acmebot.Models
{
    public class CertificatePolicyItem : IValidatableObject
    {
        [JsonProperty("certificateName")]
        [RegularExpression("^[0-9a-zA-Z-]+$")]
        public string CertificateName { get; set; }

        [JsonProperty("dnsNames")]
        public string[] DnsNames { get; set; }

        [JsonProperty("containerAppId")]
        public string ContainerAppId { get; set; }

        [JsonProperty("containerAppDomain")]
        public string ContainerAppDomain { get; set; }

        [JsonProperty("environmentId")]
        public string EnvironmentId { get; set; }

        [JsonProperty("keyType")]
        [RegularExpression("^(RSA|EC)$")]
        public string KeyType { get; set; }

        [JsonProperty("keySize")]
        public int? KeySize { get; set; }

        [JsonProperty("keyCurveName")]
        [RegularExpression(@"^P\-(256|384|521|256K)$")]
        public string KeyCurveName { get; set; }

        [JsonProperty("reuseKey")]
        public bool ReuseKey { get; set; } = false;

        [JsonProperty("expiring")]
        public bool Expiring { get; set; } = false;

        [JsonProperty("friendlyName")]
        public string FriendlyName { get; set; }

        [JsonProperty("notAfter")]
        public DateTimeOffset notAfter { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (DnsNames == null || DnsNames.Length == 0)
            {
                yield return new ValidationResult($"The {nameof(DnsNames)} is required.", new[] { nameof(DnsNames) });
            }
        }
    }
}
