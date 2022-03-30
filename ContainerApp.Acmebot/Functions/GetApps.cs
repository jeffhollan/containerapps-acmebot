using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Azure.WebJobs.Extensions.HttpApi;

using DurableTask.TypedProxy;

using ContainerApp.Acmebot.Internal;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ContainerApp.Acmebot.Functions
{
    public class GetApps : HttpFunctionBase
    {
        public GetApps(IHttpContextAccessor httpContextAccessor)
            : base(httpContextAccessor)
        {
        }

        [FunctionName(nameof(GetApps) + "_" + nameof(Orchestrator))]
        public async Task<JArray> Orchestrator([OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            var activity = context.CreateActivityProxy<ISharedActivity>();

            return JArray.Parse(await activity.GetApps());
        }

        [FunctionName(nameof(GetApps) + "_" + nameof(HttpStart))]
        public async Task<IActionResult> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "api/apps")] HttpRequest req,
            [DurableClient] IDurableClient starter,
            ILogger log)
        {
            if (!User.IsAppAuthorized())
            {
                return Unauthorized();
            }

            // Function input comes from the request content.
            var instanceId = await starter.StartNewAsync(nameof(GetApps) + "_" + nameof(Orchestrator));

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            return await starter.WaitForCompletionOrCreateCheckStatusResponseAsync(req, instanceId, TimeSpan.FromMinutes(1), returnInternalServerErrorOnFailure: true);
        }
    }
}
