using System.Threading;
using System.Threading.Tasks;

using DurableTask.TypedProxy;

using ContainerApp.Acmebot.Models;

using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using System.Security.Cryptography;
using System;
using System.Collections.Generic;

namespace ContainerApp.Acmebot.Functions
{
    public class SharedOrchestrator
    {
        [FunctionName(nameof(IssueCertificate))]
        public async Task IssueCertificate([OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            var certificatePolicy = context.GetInput<CertificatePolicyItem>();

            var activity = context.CreateActivityProxy<ISharedActivity>();

            // 前提条件をチェック
            await activity.Dns01Precondition(certificatePolicy.DnsNames);

            // 新しく ACME Order を作成する
            var orderDetails = await activity.Order(certificatePolicy.DnsNames);

            // 既に確認済みの場合は Challenge をスキップする
            if (orderDetails.Payload.Status != "ready")
            {
                // ACME Challenge を実行
                var (challengeResults, propagationSeconds) = await activity.Dns01Authorization(orderDetails.Payload.Authorizations);

                // DNS Provider が指定した分だけ遅延させる
                await context.CreateTimer(context.CurrentUtcDateTime.AddSeconds(propagationSeconds), CancellationToken.None);

                // DNS で正しくレコードが引けるか確認
                await activity.CheckDnsChallenge(challengeResults);

                // ACME Answer を実行
                await activity.AnswerChallenges(challengeResults);

                // Order のステータスが ready になるまで 60 秒待機
                await activity.CheckIsReady((orderDetails, challengeResults));

                // 作成した DNS レコードを削除
                await activity.CleanupDnsChallenge(challengeResults);
            }

            // Key Vault で CSR を作成し Finalize を実行
            (orderDetails, var rsaParameters) = await activity.FinalizeOrder((certificatePolicy, orderDetails));

            // Finalize の時点でステータスが valid の時点はスキップ
            if (orderDetails.Payload.Status != "valid")
            {
                // Finalize 後のステータスが valid になるまで 60 秒待機
                orderDetails = await activity.CheckIsValid(orderDetails);
            }

            // 証明書をダウンロードし Key Vault に保存
            (string friendlyName, DateTimeOffset notAfter, string dnsNames) = await activity.UploadCertificate((certificatePolicy, orderDetails, rsaParameters));

            var (acaChallengeResult, acaPropagationSeconds) = await activity.DnsContainerAppAuth(certificatePolicy);
            // DNS Provider が指定した分だけ遅延させる
            await context.CreateTimer(context.CurrentUtcDateTime.AddSeconds(acaPropagationSeconds), CancellationToken.None);

            // DNS で正しくレコードが引けるか確認
            await activity.CheckDnsChallenge(new List<AcmeChallengeResult> { acaChallengeResult });

            await activity.BindContainerAppToDomain(certificatePolicy);

            await activity.CleanupDnsChallenge(new List<AcmeChallengeResult> { acaChallengeResult });

            // 証明書の更新が完了後に Webhook を送信する
            await activity.SendCompletedEvent((friendlyName, notAfter, dnsNames));
        }
    }
}
