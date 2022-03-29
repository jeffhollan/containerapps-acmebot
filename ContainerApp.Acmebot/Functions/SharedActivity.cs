using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

using ACMESharp.Authorizations;
using ACMESharp.Protocol;
using ACMESharp.Protocol.Resources;

using DnsClient;

using ContainerApp.Acmebot.Internal;
using ContainerApp.Acmebot.Models;
using ContainerApp.Acmebot.Options;
using ContainerApp.Acmebot.Providers;

using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Newtonsoft.Json;
using System.Security.Cryptography;
using ACMESharp.Crypto;

namespace ContainerApp.Acmebot.Functions
{
    public class SharedActivity : ISharedActivity
    {
        public SharedActivity(LookupClient lookupClient, AcmeProtocolClientFactory acmeProtocolClientFactory,
                              IDnsProvider dnsProvider, ContainerAppClient containerAppClient,
                              WebhookInvoker webhookInvoker, IOptions<AcmebotOptions> options, ILogger<SharedActivity> logger)
        {
            _acmeProtocolClientFactory = acmeProtocolClientFactory;
            _dnsProvider = dnsProvider;
            _lookupClient = lookupClient;
            _containerAppClient = containerAppClient;
            _webhookInvoker = webhookInvoker;
            _options = options.Value;
            _logger = logger;
        }

        private readonly LookupClient _lookupClient;
        private readonly AcmeProtocolClientFactory _acmeProtocolClientFactory;
        private readonly IDnsProvider _dnsProvider;
        private readonly ContainerAppClient _containerAppClient;
        private readonly WebhookInvoker _webhookInvoker;
        private readonly AcmebotOptions _options;
        private readonly ILogger<SharedActivity> _logger;

        private const string IssuerName = "Acmebot";

        [FunctionName(nameof(GetExpiringCertificates))]
        public async Task<IReadOnlyList<X509Certificate2>> GetExpiringCertificates([ActivityTrigger] DateTime currentDateTime)
        {
            var certificates = await _containerAppClient.GetCertificatesAsync();

            var result = new List<X509Certificate2>();

            foreach (var certificate in certificates)
            {
                var x509cert = new X509Certificate2(certificate.value, _options.Password);

                if (!x509cert.IssuerName.Name.Equals(IssuerName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if ((x509cert.NotAfter - currentDateTime).TotalDays > _options.RenewBeforeExpiry)
                {
                    continue;
                }

                result.Add(x509cert);
            }

            return result;
        }

        [FunctionName(nameof(GetAllCertificates))]
        public async Task<IReadOnlyList<X509Certificate2>> GetAllCertificates([ActivityTrigger] object input = null)
        {
            var certificates = await _containerAppClient.GetCertificatesAsync();

            var result = new List<X509Certificate2>();

            foreach (var certificate in certificates)
            {
                var x509cert = new X509Certificate2(certificate.value, _options.Password);

                result.Add(x509cert);
            }

            return result;
        }

        [FunctionName(nameof(GetZones))]
        public async Task<IReadOnlyList<string>> GetZones([ActivityTrigger] object input = null)
        {
            try
            {
                var zones = await _dnsProvider.ListZonesAsync();

                return zones.Select(x => x.Name).ToArray();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        // [FunctionName(nameof(GetCertificatePolicy))]
        // public async Task<CertificatePolicyItem> GetCertificatePolicy([ActivityTrigger] string certificateName)
        // {
        //     CertificatePolicy certificatePolicy = await _certificateClient.GetCertificatePolicyAsync(certificateName);

        //     var dnsNames = certificatePolicy.SubjectAlternativeNames.DnsNames.ToArray();

        //     return new CertificatePolicyItem
        //     {
        //         CertificateName = certificateName,
        //         DnsNames = dnsNames.Length > 0 ? dnsNames : new[] { certificatePolicy.Subject[3..] },
        //         KeyType = certificatePolicy.KeyType?.ToString(),
        //         KeySize = certificatePolicy.KeySize,
        //         KeyCurveName = certificatePolicy.KeyCurveName?.ToString(),
        //         ReuseKey = certificatePolicy.ReuseKey
        //     };
        // }

        [FunctionName(nameof(RevokeCertificate))]
        public async Task RevokeCertificate([ActivityTrigger] string certificateName)
        {
            var response = await _containerAppClient.GetCertificateAsync(certificateName);

            var x509cert = new X509Certificate2(response.value, _options.Password);

            var acmeProtocolClient = await _acmeProtocolClientFactory.CreateClientAsync();

            await acmeProtocolClient.RevokeCertificateAsync(x509cert.GetRawCertData());
        }

        [FunctionName(nameof(Order))]
        public async Task<OrderDetails> Order([ActivityTrigger] IReadOnlyList<string> dnsNames)
        {
            var acmeProtocolClient = await _acmeProtocolClientFactory.CreateClientAsync();

            return await acmeProtocolClient.CreateOrderAsync(dnsNames);
        }

        [FunctionName(nameof(Dns01Precondition))]
        public async Task Dns01Precondition([ActivityTrigger] IReadOnlyList<string> dnsNames)
        {
            // DNS zone が存在するか確認
            var zones = await _dnsProvider.ListZonesAsync();

            var foundZones = new HashSet<DnsZone>();
            var zoneNotFoundDnsNames = new List<string>();

            foreach (var dnsName in dnsNames)
            {
                var zone = zones.Where(x => string.Equals(dnsName, x.Name, StringComparison.OrdinalIgnoreCase) || dnsName.EndsWith($".{x.Name}", StringComparison.OrdinalIgnoreCase))
                                .OrderByDescending(x => x.Name.Length)
                                .FirstOrDefault();

                // マッチする DNS zone が見つからない場合はエラー
                if (zone == null)
                {
                    zoneNotFoundDnsNames.Add(dnsName);
                    continue;
                }

                foundZones.Add(zone);
            }

            if (zoneNotFoundDnsNames.Count > 0)
            {
                throw new PreconditionException($"DNS zone(s) are not found. DnsNames = {string.Join(",", zoneNotFoundDnsNames)}");
            }

            // DNS zone に移譲されている Name servers が正しいか検証
            foreach (var zone in foundZones)
            {
                // DNS provider が Name servers を返していなければスキップ
                if (zone.NameServers == null || zone.NameServers.Count == 0)
                {
                    continue;
                }

                // DNS provider が Name servers を返している場合は NS レコードを確認
                var queryResult = await _lookupClient.QueryAsync(zone.Name, QueryType.NS);

                // 最後の . が付いている場合があるので削除して統一
                var expectedNameServers = zone.NameServers
                                              .Select(x => x.TrimEnd('.'))
                                              .ToArray();

                var actualNameServers = queryResult.Answers
                                                   .OfType<DnsClient.Protocol.NsRecord>()
                                                   .Select(x => x.NSDName.Value.TrimEnd('.'))
                                                   .ToArray();

                // 処理対象の DNS zone から取得した NS と実際に引いた NS の値が一つも一致しない場合はエラー
                if (!actualNameServers.Intersect(expectedNameServers, StringComparer.OrdinalIgnoreCase).Any())
                {
                    throw new PreconditionException($"The delegated name server is not correct. DNS zone = {zone.Name}, Expected = {string.Join(",", expectedNameServers)}, Actual = {string.Join(",", actualNameServers)}");
                }
            }
        }

        [FunctionName(nameof(Dns01Authorization))]
        public async Task<(IReadOnlyList<AcmeChallengeResult>, int)> Dns01Authorization([ActivityTrigger] IReadOnlyList<string> authorizationUrls)
        {
            var acmeProtocolClient = await _acmeProtocolClientFactory.CreateClientAsync();

            var challengeResults = new List<AcmeChallengeResult>();

            foreach (var authorizationUrl in authorizationUrls)
            {
                // Authorization の詳細を取得
                var authorization = await acmeProtocolClient.GetAuthorizationDetailsAsync(authorizationUrl);

                // DNS-01 Challenge の情報を拾う
                var challenge = authorization.Challenges.First(x => x.Type == "dns-01");

                var challengeValidationDetails = AuthorizationDecoder.ResolveChallengeForDns01(authorization, challenge, acmeProtocolClient.Signer);

                // Challenge の情報を保存する
                challengeResults.Add(new AcmeChallengeResult
                {
                    Url = challenge.Url,
                    DnsRecordName = challengeValidationDetails.DnsRecordName,
                    DnsRecordValue = challengeValidationDetails.DnsRecordValue
                });
            }

            // DNS zone の一覧を取得する
            var zones = await _dnsProvider.ListZonesAsync();

            // DNS-01 の検証レコード名毎に DNS に TXT レコードを作成
            foreach (var lookup in challengeResults.ToLookup(x => x.DnsRecordName))
            {
                var dnsRecordName = lookup.Key;

                var zone = zones.Where(x => dnsRecordName.EndsWith($".{x.Name}", StringComparison.OrdinalIgnoreCase))
                                .OrderByDescending(x => x.Name.Length)
                                .First();

                // Challenge の詳細から DNS 向けにレコード名を作成
                var acmeDnsRecordName = dnsRecordName.Replace($".{zone.Name}", "", StringComparison.OrdinalIgnoreCase);

                await _dnsProvider.DeleteTxtRecordAsync(zone, acmeDnsRecordName);
                await _dnsProvider.CreateTxtRecordAsync(zone, acmeDnsRecordName, lookup.Select(x => x.DnsRecordValue));
            }

            return (challengeResults, _dnsProvider.PropagationSeconds);
        }

        [FunctionName(nameof(CheckDnsChallenge))]
        public async Task CheckDnsChallenge([ActivityTrigger] IReadOnlyList<AcmeChallengeResult> challengeResults)
        {
            foreach (var challengeResult in challengeResults)
            {
                IDnsQueryResponse queryResult;

                try
                {
                    // 実際に ACME の TXT レコードを引いて確認する
                    queryResult = await _lookupClient.QueryAsync(challengeResult.DnsRecordName, QueryType.TXT);
                }
                catch (DnsResponseException ex)
                {
                    // 一時的な DNS エラーの可能性があるためリトライ
                    throw new RetriableActivityException($"{challengeResult.DnsRecordName} bad response. Message: \"{ex.DnsError}\"", ex);
                }

                var txtRecords = queryResult.Answers
                                            .OfType<DnsClient.Protocol.TxtRecord>()
                                            .ToArray();

                // レコードが存在しなかった場合はエラー
                if (txtRecords.Length == 0)
                {
                    throw new RetriableActivityException($"{challengeResult.DnsRecordName} did not resolve.");
                }

                // レコードに今回のチャレンジが含まれていない場合もエラー
                if (!txtRecords.Any(x => x.Text.Contains(challengeResult.DnsRecordValue)))
                {
                    throw new RetriableActivityException($"{challengeResult.DnsRecordName} is not correct. Expected: \"{challengeResult.DnsRecordValue}\", Actual: \"{string.Join(",", txtRecords.SelectMany(x => x.Text))}\"");
                }
            }
        }

        [FunctionName(nameof(AnswerChallenges))]
        public async Task AnswerChallenges([ActivityTrigger] IReadOnlyList<AcmeChallengeResult> challengeResults)
        {
            var acmeProtocolClient = await _acmeProtocolClientFactory.CreateClientAsync();

            // Answer の準備が出来たことを通知
            foreach (var challengeResult in challengeResults)
            {
                await acmeProtocolClient.AnswerChallengeAsync(challengeResult.Url);
            }
        }

        [FunctionName(nameof(CheckIsReady))]
        public async Task CheckIsReady([ActivityTrigger] (OrderDetails, IReadOnlyList<AcmeChallengeResult>) input)
        {
            var (orderDetails, challengeResults) = input;

            var acmeProtocolClient = await _acmeProtocolClientFactory.CreateClientAsync();

            orderDetails = await acmeProtocolClient.GetOrderDetailsAsync(orderDetails.OrderUrl, orderDetails);

            if (orderDetails.Payload.Status == "pending" || orderDetails.Payload.Status == "processing")
            {
                // pending か processing の場合はリトライする
                throw new RetriableActivityException($"ACME validation status is {orderDetails.Payload.Status}. It will retry automatically.");
            }

            if (orderDetails.Payload.Status == "invalid")
            {
                var problems = new List<Problem>();

                foreach (var challengeResult in challengeResults)
                {
                    var challenge = await acmeProtocolClient.GetChallengeDetailsAsync(challengeResult.Url);

                    if (challenge.Status != "invalid" || challenge.Error == null)
                    {
                        continue;
                    }

                    _logger.LogError($"ACME domain validation error: {JsonConvert.SerializeObject(challenge.Error)}");

                    problems.Add(challenge.Error);
                }

                // 全てのエラーが dns 関係の場合は Orchestrator からリトライさせる
                if (problems.All(x => x.Type == "urn:ietf:params:acme:error:dns"))
                {
                    throw new RetriableOrchestratorException("ACME validation status is invalid, but retriable error. It will retry automatically.");
                }

                // invalid の場合は最初から実行が必要なので失敗させる
                throw new InvalidOperationException($"ACME validation status is invalid. Required retry at first.\nLastError = {JsonConvert.SerializeObject(problems.Last())}");
            }
        }

        [FunctionName(nameof(FinalizeOrder))]
        public async Task<(OrderDetails, RSAParameters)> FinalizeOrder([ActivityTrigger] (CertificatePolicyItem, OrderDetails) input)
        {
            var (certificatePolicyItem, orderDetails) = input;

            var rsa = RSA.Create(2048);
            var hashAlgor = HashAlgorithmName.SHA256;

            string firstName = null;
            var sanBuilder = new SubjectAlternativeNameBuilder();
            foreach (var n in certificatePolicyItem.DnsNames)
            {
                sanBuilder.AddDnsName(n);
                if (firstName == null)
                {
                    firstName = n;
                }
            }
            if (firstName == null)
            {
                throw new ArgumentException("Must specify at least one name");
            }

            var dn = new X500DistinguishedName($"CN={firstName}");
            var csr = new CertificateRequest(dn,
                    rsa, hashAlgor, RSASignaturePadding.Pkcs1);
            csr.CertificateExtensions.Add(sanBuilder.Build());

            var builtCsr = csr.CreateSigningRequest();

            // Order の最終処理を実行する
            var acmeProtocolClient = await _acmeProtocolClientFactory.CreateClientAsync();

            return (await acmeProtocolClient.FinalizeOrderAsync(orderDetails.Payload.Finalize, builtCsr), rsa.ExportParameters(true));
        }

        [FunctionName(nameof(CheckIsValid))]
        public async Task<OrderDetails> CheckIsValid([ActivityTrigger] OrderDetails orderDetails)
        {
            var acmeProtocolClient = await _acmeProtocolClientFactory.CreateClientAsync();

            orderDetails = await acmeProtocolClient.GetOrderDetailsAsync(orderDetails.OrderUrl, orderDetails);

            if (orderDetails.Payload.Status == "pending" || orderDetails.Payload.Status == "processing")
            {
                // pending か processing の場合はリトライする
                throw new RetriableActivityException($"Finalize request is {orderDetails.Payload.Status}. It will retry automatically.");
            }

            if (orderDetails.Payload.Status == "invalid")
            {
                // invalid の場合は最初から実行が必要なので失敗させる
                throw new InvalidOperationException("Finalize request is invalid. Required retry at first.");
            }

            return orderDetails;
        }

        [FunctionName(nameof(UploadCertificate))]
        public async Task<(string, DateTimeOffset, string)> UploadCertificate([ActivityTrigger] (CertificatePolicyItem, OrderDetails, RSAParameters) input)
        {
            var (certificatePolicy, orderDetails, rsaParameters) = input;

            var acmeProtocolClient = await _acmeProtocolClientFactory.CreateClientAsync();

            // 証明書をダウンロードして Key Vault へ格納
            var x509Certificates = await acmeProtocolClient.GetOrderCertificateAsync(orderDetails, _options.PreferredChain);

            var rsa = RSA.Create(rsaParameters);

            x509Certificates[0] = x509Certificates[0].CopyWithPrivateKey(rsa);

            var pfxBlob = x509Certificates.Export(X509ContentType.Pfx, _options.Password);

            var response = await _containerAppClient.UploadCertificateAsync(certificatePolicy, pfxBlob, _options.Password);
            _logger.LogInformation($"Got response from aca: {response}");

            var cert = x509Certificates[0];
            return (cert.FriendlyName, cert.NotAfter, cert.Extensions["2.5.29.17"].ToString());
        }

        [FunctionName(nameof(CleanupDnsChallenge))]
        public async Task CleanupDnsChallenge([ActivityTrigger] IReadOnlyList<AcmeChallengeResult> challengeResults)
        {
            // DNS zone の一覧を取得する
            var zones = await _dnsProvider.ListZonesAsync();

            // DNS-01 の検証レコード名毎に DNS から TXT レコードを削除
            foreach (var lookup in challengeResults.ToLookup(x => x.DnsRecordName))
            {
                var dnsRecordName = lookup.Key;

                var zone = zones.Where(x => dnsRecordName.EndsWith($".{x.Name}", StringComparison.OrdinalIgnoreCase))
                                .OrderByDescending(x => x.Name.Length)
                                .First();

                // Challenge の詳細から DNS 向けにレコード名を作成
                var acmeDnsRecordName = dnsRecordName.Replace($".{zone.Name}", "", StringComparison.OrdinalIgnoreCase);

                await _dnsProvider.DeleteTxtRecordAsync(zone, acmeDnsRecordName);
            }
        }

        [FunctionName(nameof(SendCompletedEvent))]
        public Task SendCompletedEvent([ActivityTrigger] (string, DateTimeOffset?, string) input)
        {
            var (certificateName, expirationDate, dnsNames) = input;

            return _webhookInvoker.SendCompletedEventAsync(certificateName, expirationDate, dnsNames);
        }
        public async Task<(AcmeChallengeResult, int)> DnsContainerAppAuth(CertificatePolicyItem certificatePolicy)
        {
            var domainVerificationId = await _containerAppClient.GetDomainVerificationIdAsync(certificatePolicy.ContainerAppId);
            // DNS zone の一覧を取得する
            var zones = await _dnsProvider.ListZonesAsync();

            // TODO: support more than just one DnsNames
            var dnsName = certificatePolicy.DnsNames[0];
            var zone = zones.Where(x => dnsName.EndsWith($".{x.Name}", StringComparison.OrdinalIgnoreCase))
                                .OrderByDescending(x => x.Name.Length)
                                .First();

            var validationDnsRecordName = dnsName.Replace($".{zone.Name}", "", StringComparison.OrdinalIgnoreCase);

            await _dnsProvider.DeleteTxtRecordAsync(zone, validationDnsRecordName);
            await _dnsProvider.CreateTxtRecordAsync(zone, validationDnsRecordName, new List<string> { domainVerificationId });
            var dnsChallenge = new AcmeChallengeResult
            {
                DnsRecordName = dnsName,
                DnsRecordValue = domainVerificationId
            };

            return (dnsChallenge, _dnsProvider.PropagationSeconds);
        }
        public async Task BindContainerAppToDomain(CertificatePolicyItem certificatePolicy)
        {
            await _containerAppClient.ValidateDomainAsync(certificatePolicy.ContainerAppId, certificatePolicy.DnsNames);
            await _containerAppClient.BindDomainAsync(certificatePolicy.ContainerAppId, certificatePolicy.DnsNames, certificatePolicy.CertificateName);
        }
    }
}