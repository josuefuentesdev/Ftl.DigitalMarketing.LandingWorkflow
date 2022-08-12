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

        [FunctionName("WorkflowOrchestrator")]
        public async Task RunOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            WorkflowOrchestratorRequest request = context.GetInput<WorkflowOrchestratorRequest>();
            int contactId = request.ContactId;
            context.SetCustomStatus(new
            {
                stage = "START",
                contactId
            });


            ExecutionContactRequest executionContactRequest = new ExecutionContactRequest
            {
                ContactId = contactId,
                InstanceId = context.InstanceId
            };
            await context.CallActivityAsync("EmailSender_WelcomeEmail", executionContactRequest);


            DateTime dueTime = context.CurrentUtcDateTime.AddMinutes(5);

            var welcomeEmailBuyedEvent = context.WaitForExternalEvent<bool>("WelcomeEmail_Buyed");
            var welcomeEmailConsideredEvent = context.WaitForExternalEvent<bool>("WelcomeEmail_Considered");
            var timeoutEvent = context.CreateTimer(dueTime, CancellationToken.None);

            var welcomeEmailResult = await Task.WhenAny(welcomeEmailBuyedEvent, welcomeEmailConsideredEvent, timeoutEvent);

            if (welcomeEmailResult == timeoutEvent)
            {
                context.SetCustomStatus(new
                {
                    stage = "TIMEOUT",
                    contactId
                });
            }
            else if (welcomeEmailResult == welcomeEmailConsideredEvent)
            {
                context.SetCustomStatus(new
                {
                    stage = "CONSIDER",
                    contactId
                });
                DateTime reminderTime = context.CurrentUtcDateTime.AddMinutes(5);
                await context.CreateTimer(reminderTime, CancellationToken.None);
                await context.CallActivityAsync("EmailSender_RemainderEmail", executionContactRequest);
            }
            else if (welcomeEmailResult == welcomeEmailBuyedEvent)
            {
                context.SetCustomStatus(new
                {
                    stage = "DECISION",
                    contactId
                });
                await context.CallActivityAsync("EmailSender_OrderDetailsEmail", executionContactRequest);
            }
        }



        [FunctionName("WorkflowOrchestrator_HttpStartFromLandingPage")]
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
            await entityClient.SignalEntityAsync(entityId, "SetContactId", contactId);
            log.LogInformation($"New ContactId = '{contactId}'.");

            // Start Workflow
            WorkflowOrchestratorRequest workflowRequest = new();
            workflowRequest.ContactId = contactId;
            workflowRequest.Email = request.Email;
            workflowRequest.InstanceId = instanceId;

            string responseInstanceId = await client.StartNewAsync("WorkflowOrchestrator", instanceId, workflowRequest);
            log.LogInformation($"Started orchestration with responseID = '{responseInstanceId}'.");
            await entityClient.SignalEntityAsync(entityId, "NotifyEventType", "ORCHESTRATION_STARTED");

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

            DurableOrchestrationStatus status = await client.GetStatusAsync(instanceId);

            await entityClient.SignalEntityAsync<IExecutionContact>(instanceId,
                p => p.UpdateStage("BUY"));


            // if the workflow is completed, return the view with the already created orderId
            if (status.RuntimeStatus != OrchestrationRuntimeStatus.Completed)
            {
                if (status.CustomStatus["stage"].ToString() == "START")
                {
                    var contactId = Int32.Parse(status.CustomStatus["contactId"].ToString());
                    CreateOrderDto createOrderDto = new()
                    {
                        ContactId = contactId,
                        Status = "PENDING"
                    };
                    int orderId = await _backofficeClient.CreateOrderAsync(createOrderDto);
                    await client.RaiseEventAsync(instanceId, "WelcomeEmail_Buyed", true);
                    OrderReceived order = new()
                    {
                        OrderId = orderId,
                        LeavePageActionUrl = "https://www.samsung.com/us/smartphones/galaxy-z-fold3-5g/"
                    };
                    var invoiceHtml = await RazorTemplateEngine.RenderAsync("/Pages/OrderReceived.cshtml", order);

                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(invoiceHtml, Encoding.UTF8, "text/html")
                    };
                }
            }

            if (status.CustomStatus["stage"].ToString() == "TIMEOUT")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("<html><body>The offer has expired</body></html>", Encoding.UTF8, "text/html")
                };
            }
            else if (status.CustomStatus["stage"].ToString() == "CONSIDER")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("<html><body>You still has a chance to buy, call us to (0800) XXXXX.</body></html>", Encoding.UTF8, "text/html")
                };
            }
            else if (status.CustomStatus["stage"].ToString() == "DECISION")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("<html><body>Product was bought. Please check your email for the order details.</body></html>", Encoding.UTF8, "text/html")
                };
            }
            else
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("<html><body>Welcome Email was not bought. Please consider buying it.</body></html>", Encoding.UTF8, "text/html")
                };
            }
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

        [FunctionName("NotifyContactEvent")]
        public async Task<HttpResponseMessage> NotifyContactEvent([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestMessage req, ILogger log, [DurableClient] IDurableOrchestrationClient client)
        {
            var content = req.Content;
            string jsonContent = await content.ReadAsStringAsync();
            NotifyContactEventRequest contactEventData = JsonConvert.DeserializeObject<NotifyContactEventRequest>(jsonContent);

            DurableOrchestrationStatus status = await client.GetStatusAsync(contactEventData.InstanceId);
            if (status == null) return new HttpResponseMessage(HttpStatusCode.BadRequest);

            var contactId = Int32.Parse(status.CustomStatus["contactId"].ToString());

            CreateContactEventDto contactEventDto = new();
            contactEventDto.ContactId = contactId;
            contactEventDto.EventType = contactEventData.EventType;

            
            var _ = await _backofficeClient.CreateContactEventAsync(contactEventDto);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}
