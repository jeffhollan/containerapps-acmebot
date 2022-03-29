﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Azure.WebJobs.Extensions.HttpApi;

using DurableTask.TypedProxy;

using ContainerApp.Acmebot.Internal;
using ContainerApp.Acmebot.Models;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography.X509Certificates;

namespace ContainerApp.Acmebot.Functions
{
    public class GetCertificates : HttpFunctionBase
    {
        public GetCertificates(IHttpContextAccessor httpContextAccessor)
            : base(httpContextAccessor)
        {
        }

        [FunctionName(nameof(GetCertificates) + "_" + nameof(Orchestrator))]
        public Task<IList<ContainerAppCertificate>> Orchestrator([OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            var activity = context.CreateActivityProxy<ISharedActivity>();

            return activity.GetAllCertificates();
        }

        [FunctionName(nameof(GetCertificates) + "_" + nameof(HttpStart))]
        public async Task<IActionResult> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "api/certificates")] HttpRequest req,
            [DurableClient] IDurableClient starter,
            ILogger log)
        {
            if (!User.IsAppAuthorized())
            {
                return Unauthorized();
            }

            // Function input comes from the request content.
            var instanceId = await starter.StartNewAsync(nameof(GetCertificates) + "_" + nameof(Orchestrator));

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            return await starter.WaitForCompletionOrCreateCheckStatusResponseAsync(req, instanceId, TimeSpan.FromMinutes(1), returnInternalServerErrorOnFailure: true);
        }
    }
}
