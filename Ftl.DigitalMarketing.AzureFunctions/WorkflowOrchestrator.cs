using Ftl.DigitalMarketing.AzureFunctions.Contract.Requests;
using Ftl.DigitalMarketing.AzureFunctions.Contract.Responses;
using Ftl.DigitalMarketing.AzureFunctions.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Ftl.DigitalMarketing.AzureFunctions
{
    public class WorkflowOrchestrator
    {
        [FunctionName("WorkflowOrchestrator")]
        public static async Task<List<string>> RunOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            var outputs = new List<string>();

            WorkflowOrchestratorRequest request = context.GetInput<WorkflowOrchestratorRequest>();
            // Replace "hello" with the name of your Durable Activity Function.
            //outputs.Add(await context.CallActivityAsync<string>("WorkflowOrchestrator_Hello", "Tokyo"));
            //outputs.Add(await context.CallActivityAsync<string>("WorkflowOrchestrator_Hello", "Seattle"));

            await context.CallActivityAsync<string>("SendEmail", request);
            //bool emailOpen = await context.WaitForExternalEvent<bool>("WelcomeEmailOpen", TimeSpan.FromMinutes(10));

            //if (!emailOpen)
            //{
            //    await context.CallActivityAsync<string>("SendEmail", "Gracias por participar");
            //}



            // returns ["Hello Tokyo!", "Hello Seattle!", "Hello London!"]
            return outputs;
        }

        [FunctionName("WorkflowOrchestrator_HttpStartFromLandingPage")]
        public static async Task<HttpResponseMessage> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestMessage req,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
        {
            var content = req.Content;
            string jsonContent = await content.ReadAsStringAsync();
            EmailLeadRequest request = JsonConvert.DeserializeObject<EmailLeadRequest>(jsonContent);

            string instanceId = Guid.NewGuid().ToString();
            // do an api call to backoffice
            int contactId = 100;
            log.LogInformation($"New ContactId = '{contactId}'.");

            // Start Workflow
            WorkflowOrchestratorRequest workflowRequest = new();
            workflowRequest.ContactId = contactId;
            workflowRequest.Email = request.Email;
            string responseInstanceId = await starter.StartNewAsync("WorkflowOrchestrator", instanceId, workflowRequest);

            log.LogInformation($"Started orchestration with responseID = '{responseInstanceId}'.");


            EmailLeadResponse response = new();
            response.ContactId = contactId;

            HttpResponseMessage responseMsg = new HttpResponseMessage(HttpStatusCode.OK);
            responseMsg.Content = new StringContent(JsonConvert.SerializeObject(response));
            return responseMsg;
        }
    }
}
