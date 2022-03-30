using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using ACMESharp.Protocol;

using DurableTask.TypedProxy;

using ContainerApp.Acmebot.Models;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.IO;
using System.Text.Json.Nodes;

namespace ContainerApp.Acmebot.Functions
{
    public interface ISharedActivity
    {
        Task<IReadOnlyList<ContainerAppCertificate>> GetExpiringCertificates(DateTime currentDateTime);

        Task<IList<ContainerAppCertificate>> GetAllCertificates(object input = null);

        Task<IReadOnlyList<string>> GetZones(object input = null);

        // Task<CertificatePolicyItem> GetCertificatePolicy(string certificateName);

        //Task RevokeCertificate(string certificateName);

        Task<OrderDetails> Order(IReadOnlyList<string> dnsNames);

        Task Dns01Precondition(IReadOnlyList<string> dnsNames);
        Task<CertificatePolicyItem> MergeExistingCertificate(CertificatePolicyItem certificatePolicy);
        Task<string> GetApps(object input = null);
        Task<(IReadOnlyList<AcmeChallengeResult>, int)> Dns01Authorization(IReadOnlyList<string> authorizationUrls);

        [RetryOptions("00:00:10", 12, HandlerType = typeof(ExceptionRetryStrategy<RetriableActivityException>))]
        Task CheckDnsChallenge(IReadOnlyList<AcmeChallengeResult> challengeResults);

        Task AnswerChallenges(IReadOnlyList<AcmeChallengeResult> challengeResults);

        [RetryOptions("00:00:05", 12, HandlerType = typeof(ExceptionRetryStrategy<RetriableActivityException>))]
        Task CheckIsReady((OrderDetails, IReadOnlyList<AcmeChallengeResult>) input);

        Task<(OrderDetails, RSAParameters)> FinalizeOrder((CertificatePolicyItem, OrderDetails) input);

        [RetryOptions("00:00:05", 12, HandlerType = typeof(ExceptionRetryStrategy<RetriableActivityException>))]
        Task<OrderDetails> CheckIsValid(OrderDetails orderDetails);

        Task<CertificatePolicyItem> UploadCertificate((CertificatePolicyItem, OrderDetails, RSAParameters) input);

        Task CleanupDnsChallenge(IReadOnlyList<AcmeChallengeResult> challengeResults);

        Task SendCompletedEvent((string, DateTimeOffset?, string) input);
        Task<(AcmeChallengeResult, int)> DnsContainerAppAuth(CertificatePolicyItem certificatePolicy);
        Task BindContainerAppToDomain(CertificatePolicyItem certificatePolicy);
    }
}
