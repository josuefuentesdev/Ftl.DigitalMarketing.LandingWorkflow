using Ftl.DigitalMarketing.ApiClientServices;
using Ftl.DigitalMarketing.AzureFunctions.Contract.Requests;
using Ftl.DigitalMarketing.AzureFunctions.Contract.Responses;
using Ftl.DigitalMarketing.AzureFunctions.DurableEntities;
using Ftl.DigitalMarketing.AzureFunctions.Models;
using Ftl.DigitalMarketing.RazorTemplates.Pages;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Razor.Templating.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ftl.DigitalMarketing.AzureFunctions
{
    public class WorkflowOrchestrator
    {
        private readonly HttpClient _http;
        private BackofficeApiClient _backofficeClient;

        public WorkflowOrchestrator(HttpClient http)
        {
            _http = http;
            _backofficeClient = new("https://localhost:5001", _http);
        }

        [FunctionName("WelcomeOrchestrator")]
        public async Task WelcomeOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context
        )
        {
            ExecutionContactRequest executionContactRequest = context.GetInput<ExecutionContactRequest>();
            await context.CallActivityAsync("EmailSender_WelcomeEmail", executionContactRequest);
        }

        [FunctionName("DecisionOrchestrator")]
        public async Task DecisionOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context
        )
        {
            ExecutionContactRequest executionContactRequest = context.GetInput<ExecutionContactRequest>();
            await context.CallActivityAsync("EmailSender_OrderDetailsEmail", executionContactRequest);
        }

        [FunctionName("ExpiredOfferOrchestrator")]
        public async Task ExpiredOfferOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context,
            [DurableClient] IDurableEntityClient entityClient
        )
        {
            ExpireOfferRequest request = context.GetInput<ExpireOfferRequest>();

            // wait until offert expire
            DateTime deadline = context.CurrentUtcDateTime.Add(request.WaitFor);
            await context.CreateTimer(deadline, CancellationToken.None);

            var stateResponse = await entityClient.ReadEntityStateAsync<ExecutionContact>(
                new EntityId("ExecutionContact", request.InstanceId));

            // only notify if the client didn't buy
            if (stateResponse.EntityState.Stage != "DECISION")
            {
                await context.CallActivityAsync("EmailSender_ExpiredOffer", stateResponse.EntityState.ContactId);
            }
            
        }


        [FunctionName("HttpStartFromLandingPage")]
        public async Task<HttpResponseMessage> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestMessage req,
            [DurableClient] IDurableOrchestrationClient client,
            [DurableClient] IDurableEntityClient entityClient,
            ILogger log)
        {
            var content = req.Content;
            string jsonContent = await content.ReadAsStringAsync();
            EmailLeadRequest request = JsonConvert.DeserializeObject<EmailLeadRequest>(jsonContent);

            CreateContactDto contact = new()
            {
                Email = request.Email
            };
            var contactId = await _backofficeClient.CreateContactAsync(contact);
            
            string instanceId = Guid.NewGuid().ToString();
            var entityId = new EntityId("ExecutionContact", instanceId);
            log.LogInformation($"ExecutionContact id= '{entityId}'.");
            
            await entityClient.SignalEntityAsync(entityId, "SetContactId", contactId);
            log.LogInformation($"New ContactId = '{contactId}'.");

            HttpResponseMessage responseMsg = new HttpResponseMessage(HttpStatusCode.OK);
            EmailLeadResponse response = new();
            response.ContactId = contactId;
            responseMsg.Content = new StringContent(JsonConvert.SerializeObject(response));
            return responseMsg;
        }

        [FunctionName("WelcomeEmail_BuyAction")]
        public async Task<HttpResponseMessage> BuyAction(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req,
            [DurableClient] IDurableOrchestrationClient client,
            [DurableClient] IDurableEntityClient entityClient,
            ILogger log)
        {
            string instanceId = req.Query["instanceId"];
            var entityId = new EntityId("ExecutionContact", instanceId);

            await entityClient.SignalEntityAsync<IExecutionContact>(entityId,
                p => p.UpdateStage("DECISION"));

            var stateResponse = await entityClient.ReadEntityStateAsync<ExecutionContact>(
                entityId);
            int orderId = stateResponse.EntityState.OrderId;
            bool ExpiredOffer = stateResponse.EntityState.ExpiredOffer;
            string stage = stateResponse.EntityState.Stage;

            var stringContent = "<html><body>Something fail</body></html>";

            if (ExpiredOffer)
            {
                stringContent = "<html><body>The offer has expired</body></html>";
            }
            // even if the offer expired, we send the last order for the current execution
            if (orderId != 0 && stage == "DECISION")
            {
                OrderReceived order = new()
                {
                    OrderId = orderId,
                    LeavePageActionUrl = "https://www.samsung.com/us/smartphones/galaxy-z-fold3-5g/"
                };
                stringContent = await RazorTemplateEngine.RenderAsync("/Pages/OrderReceived.cshtml", order);
            } else
            {
                stringContent = "<html><body>Your order was placed, review your email for more details.</body></html>";
            }


            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(stringContent, Encoding.UTF8, "text/html")
            };

        }

        [FunctionName("WelcomeEmail_ConsiderAction")]
        public async Task<HttpResponseMessage> ConsiderAction(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req,
            [DurableClient] IDurableOrchestrationClient client,
            ILogger log)
        {
            string instanceId = req.Query["instanceId"];

            await client.RaiseEventAsync(instanceId, "WelcomeEmail_Considered", true);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<html><body>We will remaind you soon, stay tuned.</body></html>", Encoding.UTF8, "text/html")
            };
        }

        [FunctionName("UnsubscribeAction")]
        public async Task<HttpResponseMessage> UnsubscribeAction(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req,
            [DurableClient] IDurableOrchestrationClient client,
            ILogger log)
        {
            string instanceId = req.Query["instanceId"];

            
            await client.TerminateAsync(instanceId, "ClientUnsubscribe");

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<html><body>We will no longer send any marketing emails, only transactional ones when there are solicited.</body></html>", Encoding.UTF8, "text/html")
            };
        }

    }
}
