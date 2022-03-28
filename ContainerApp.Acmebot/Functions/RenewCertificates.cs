using System;
using System.Threading;
using System.Threading.Tasks;

using ContainerApp.Acmebot.Models;

using DurableTask.TypedProxy;

using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;

namespace ContainerApp.Acmebot.Functions
{
    public class RenewCertificates
    {
        [FunctionName(nameof(RenewCertificates) + "_" + nameof(Orchestrator))]
        public async Task Orchestrator([OrchestrationTrigger] IDurableOrchestrationContext context, ILogger log)
        {
            var activity = context.CreateActivityProxy<ISharedActivity>();

            // 期限切れまで 30 日以内の証明書を取得する
            var certificates = await activity.GetExpiringCertificates(context.CurrentUtcDateTime);

            // 更新対象となる証明書がない場合は終わる
            if (certificates.Count == 0)
            {
                log.LogInformation("No expiring certificates found");

                return;
            }

            // スロットリング対策として 120 秒以内でジッターを追加する
            var jitter = (uint)context.NewGuid().GetHashCode() % 120;

            await context.CreateTimer(context.CurrentUtcDateTime.AddSeconds(jitter), CancellationToken.None);

            // 証明書の更新を行う
            foreach (var certificate in certificates)
            {
                log.LogInformation($"{certificate.FriendlyName} - {certificate.NotAfter}");

                try
                {
                    // 証明書の更新処理を開始
                    // var certificatePolicyItem = await activity.GetCertificatePolicy(certificate.Name);
                    // TODO: Implement fetch certificate by certificate - or at least the pattern
                    var certificatePolicyItem = new CertificatePolicyItem();

                    await context.CallSubOrchestratorWithRetryAsync(nameof(SharedOrchestrator.IssueCertificate), _retryOptions, certificatePolicyItem);
                }
                catch (Exception ex)
                {
                    // 失敗した場合はログに詳細を書き出して続きを実行する
                    log.LogError($"Failed sub orchestration with DNS names = {string.Join(",", certificate.Extensions["2.5.29.17"])}");
                    log.LogError(ex.Message);
                }
            }
        }

        [FunctionName(nameof(RenewCertificates) + "_" + nameof(Timer))]
        public async Task Timer([TimerTrigger("0 0 0 * * *")] TimerInfo timer, [DurableClient] IDurableClient starter, ILogger log)
        {
            // Function input comes from the request content.
            var instanceId = await starter.StartNewAsync(nameof(RenewCertificates) + "_" + nameof(Orchestrator));

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");
        }

        private readonly RetryOptions _retryOptions = new RetryOptions(TimeSpan.FromHours(3), 2)
        {
            Handle = ex => ex.InnerException?.InnerException is RetriableOrchestratorException
        };
    }
}
