using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ACMESharp.Protocol.Resources;

namespace ContainerApp.Acmebot.Models
{
    public class ContainerAppCertificate
    {
        public string certificateName { get; set; }
        public string subjectName { get; set; }
        public string dnsNames { get; set; }
        public string issuer { get; set; }
        public DateTime expirationDate { get; set; }
        public string provisioningState { get; set; }
        public string environmentId { get; set; }
    }
}
