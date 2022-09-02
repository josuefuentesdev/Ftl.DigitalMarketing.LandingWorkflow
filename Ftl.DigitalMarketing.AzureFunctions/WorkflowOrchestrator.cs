using Ftl.DigitalMarketing.ApiClientServices;
using Ftl.DigitalMarketing.AzureFunctions.Contract.Responses;
using Ftl.DigitalMarketing.AzureFunctions.DurableEntities;
using Ftl.DigitalMarketing.AzureFunctions.Models;
using Ftl.DigitalMarketing.AzureFunctions.Services.Recaptcha;
using Ftl.DigitalMarketing.RazorTemplates.Emails;
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
        private IRecaptchaVerifierService recaptchaVerifierService;

        public WorkflowOrchestrator(HttpClient http, IRecaptchaVerifierService recaptchaVerifierService)
        {
            _http = http;
            _backofficeClient = new(Environment.GetEnvironmentVariable("BACKOFFICE_URL"), _http);
            this.recaptchaVerifierService = recaptchaVerifierService;
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
            OrderDetailsEmailModel request = context.GetInput<OrderDetailsEmailModel>();
            bool emailSend = await context.CallActivityAsync<bool>("EmailSender_OrderDetailsEmail", request);
            if (emailSend)
            {
                await _backofficeClient.UpdateOrderAsync(request.OrderId, new UpdateOrderDto() { Status = "SENT" });
            }
        }

        [FunctionName("ConsiderOrchestrator")]
        public async Task ConsiderOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context,
            [DurableClient] IDurableEntityClient entityClient
        )
        {
            ExecutionContactRequest request = context.GetInput<ExecutionContactRequest>();
            string instanceId = request.InstanceId;
            var entityId = new EntityId("ExecutionContact", instanceId);
          
            // wait a time to send the offer again
            DateTime deadline = context.CurrentUtcDateTime.Add(TimeSpan.FromMinutes(5));
            await context.CreateTimer(deadline, CancellationToken.None);

            var stateResponse = await entityClient.ReadEntityStateAsync<ExecutionContact>(entityId);
            bool ExpiredOffer = stateResponse.EntityState.ExpiredOffer;

            if (!ExpiredOffer)
            {
                ExecutionContactRequest requestEmail = context.GetInput<ExecutionContactRequest>();
                await context.CallActivityAsync("EmailSender_RemainderEmail", requestEmail);
            }
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

            context.SignalEntity(new EntityId("ExecutionContact", request.InstanceId),"expireOffer");
        }


        [FunctionName("HttpStartFromLandingPage")]
        public async Task<HttpResponseMessage> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestMessage req,
            [DurableClient] IDurableOrchestrationClient client,
            [DurableClient] IDurableEntityClient entityClient,
            ILogger log)
        {
            var formdata = await req.Content.ReadAsFormDataAsync();
            string email = formdata["email"];
            string recaptchaResponse = formdata["g-recaptcha-response"];
            //EmailLeadRequest request = JsonConvert.DeserializeObject<EmailLeadRequest>(jsonContent);

            // validate recatpcha
            //bool isValidRequest = await recaptchaVerifierService.ValidatedRequest(recaptchaResponse);
            //if(!isValidRequest)
            //{
            //    HttpResponseMessage errorResponseMsg = new HttpResponseMessage(HttpStatusCode.BadRequest);
            //    EmailLeadResponse errorResponse = new();
            //    errorResponse.Success = false;
            //    errorResponse.Message = "bots not allowed";
            //    errorResponseMsg.Content = new StringContent(JsonConvert.SerializeObject(errorResponse));
            //    return errorResponseMsg;
            //}

            CreateContactDto contact = new()
            {
                Email = email
            };
            var contactId = await _backofficeClient.CreateContactAsync(contact);
            
            string instanceId = Guid.NewGuid().ToString();
            var entityId = new EntityId("ExecutionContact", instanceId);
            log.LogInformation($"ExecutionContact id= '{entityId}'.");
            
            await entityClient.SignalEntityAsync(entityId, "SetContactId", contactId);
            log.LogInformation($"New ContactId = '{contactId}'.");

            HttpResponseMessage responseMsg = new HttpResponseMessage(HttpStatusCode.OK);
            EmailLeadResponse response = new();
            response.Success = true;
            response.Message = "OK";
            //response.ContactId = contactId;
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

            var stateResponse = await entityClient.ReadEntityStateAsync<ExecutionContact>(
                entityId);
            int contactId = stateResponse.EntityState.ContactId;
            int orderId = stateResponse.EntityState.OrderId;
            bool ExpiredOffer = stateResponse.EntityState.ExpiredOffer;
            string stage = stateResponse.EntityState.Stage;

            var stringContent = "<html><body>Something fail</body></html>";

            if (ExpiredOffer)
            {
                stringContent = "<html><body>The offer has expired</body></html>";
            }
            // create the order
            if (stage != "DECISION" && !ExpiredOffer)
            {
                CreateOrderDto createOrderDto = new()
                {
                    ContactId = contactId,
                    Status = "PENDING",
                    NetPrice = 100.00
                };
                var newOrderId = await _backofficeClient.CreateOrderAsync(createOrderDto);

                await entityClient.SignalEntityAsync<IExecutionContact>(entityId,
                    p => p.SetOrderId(newOrderId));
                
                OrderReceived order = new()
                {
                    OrderId = newOrderId,
                    LeavePageActionUrl = "https://www.samsung.com/us/smartphones/galaxy-z-fold3-5g/"
                };
                stringContent = await RazorTemplateEngine.RenderAsync("/Pages/OrderReceived.cshtml", order);
            } 
            // even if the offer expired, we send the last order for the current execution
            else if (stage == "DECISION" && orderId != 0)
            {
                OrderReceived order = new()
                {
                    OrderId = orderId,
                    LeavePageActionUrl = "https://www.samsung.com/us/smartphones/galaxy-z-fold3-5g/"
                };
                stringContent = await RazorTemplateEngine.RenderAsync("/Pages/OrderReceived.cshtml", order);
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
            [DurableClient] IDurableEntityClient entityClient,
            ILogger log)
        {
            string instanceId = req.Query["instanceId"];
            var entityId = new EntityId("ExecutionContact", instanceId);
            var stateResponse = await entityClient.ReadEntityStateAsync<ExecutionContact>(entityId);
            bool ExpiredOffer = stateResponse.EntityState.ExpiredOffer;
            string stage = stateResponse.EntityState.Stage;


            var stringContent = "The offer has expired";
            
            if (!ExpiredOffer)
            {
                stringContent = "<html><body>We will remaind you soon, stay tuned.</body></html>";
                if (stage == "START")
                {
                    await entityClient.SignalEntityAsync<IExecutionContact>(entityId,
                        p => p.UpdateStage("CONSIDER"));
                }
            }
            
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(stringContent, Encoding.UTF8, "text/html")
            };
        }

        [FunctionName("UnsubscribeAction")]
        public async Task<HttpResponseMessage> UnsubscribeAction(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req,
            [DurableClient] IDurableOrchestrationClient client,
            ILogger log)
        {
            string instanceId = req.Query["instanceId"];

            // TODO: stop all schedule work and add client to blocklist outbound email

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<html><body>We will no longer send any marketing emails, only transactional ones when there are solicited.</body></html>", Encoding.UTF8, "text/html")
            };
        }

    }
}
