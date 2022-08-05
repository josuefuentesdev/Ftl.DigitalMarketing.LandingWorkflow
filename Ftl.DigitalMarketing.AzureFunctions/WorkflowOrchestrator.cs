using Ftl.DigitalMarketing.AzureFunctions.Contract.Requests;
using Ftl.DigitalMarketing.AzureFunctions.Contract.Responses;
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
        [FunctionName("WorkflowOrchestrator")]
        public static async Task RunOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            context.SetCustomStatus(new
            {
                stage = "START"
            });

            WorkflowOrchestratorRequest request = context.GetInput<WorkflowOrchestratorRequest>();

            await context.CallActivityAsync("EmailSender_WelcomeEmail", request);


            DateTime dueTime = context.CurrentUtcDateTime.AddMinutes(5);

            var welcomeEmailBuyedEvent = context.WaitForExternalEvent<bool>("WelcomeEmail_Buyed");
            var welcomeEmailConsideredEvent = context.WaitForExternalEvent<bool>("WelcomeEmail_Considered");
            var timeoutEvent = context.CreateTimer(dueTime, CancellationToken.None);

            var welcomeEmailResult = await Task.WhenAny(welcomeEmailBuyedEvent, welcomeEmailConsideredEvent, timeoutEvent);

            if (welcomeEmailResult == timeoutEvent)
            {
                context.SetCustomStatus(new
                {
                    stage = "TIMEOUT"
                });
            }
            else if (welcomeEmailResult == welcomeEmailConsideredEvent)
            {
                context.SetCustomStatus(new
                {
                    stage = "CONSIDER"
                });
                DateTime reminderTime = context.CurrentUtcDateTime.AddMinutes(5);
                await context.CreateTimer(reminderTime, CancellationToken.None);
                await context.CallActivityAsync("EmailSender_RemainderEmail", request);
            }
            else if (welcomeEmailResult == welcomeEmailBuyedEvent)
            {
                context.SetCustomStatus(new
                {
                    stage = "DECISION"
                });
                await context.CallActivityAsync("EmailSender_OrderDetailsEmail", request);
            }
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
            workflowRequest.InstanceId = instanceId;

            string responseInstanceId = await starter.StartNewAsync("WorkflowOrchestrator", instanceId, workflowRequest);

            log.LogInformation($"Started orchestration with responseID = '{responseInstanceId}'.");


            EmailLeadResponse response = new();
            response.ContactId = contactId;

            HttpResponseMessage responseMsg = new HttpResponseMessage(HttpStatusCode.OK);
            responseMsg.Content = new StringContent(JsonConvert.SerializeObject(response));
            return responseMsg;
        }

        [FunctionName("WelcomeEmail_BuyAction")]
        public static async Task<HttpResponseMessage> BuyAction(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req,
            [DurableClient] IDurableOrchestrationClient client,
            ILogger log)
        {
            string instanceId = req.Query["instanceId"];

            DurableOrchestrationStatus status = await client.GetStatusAsync(instanceId);

            // if the workflow is completed, return the view with the already created orderId
            if (status.RuntimeStatus != OrchestrationRuntimeStatus.Completed)
            {
                if (status.CustomStatus["stage"].ToString() == "START")
                {

                    await client.RaiseEventAsync(instanceId, "WelcomeEmail_Buyed", true);
                    OrderReceived order = new()
                    {
                        OrderId = 12345,
                        LeavePageActionUrl = "https://www.samsung.com/us/smartphones/galaxy-z-fold3-5g/"
                    };
                    var invoiceHtml = await RazorTemplateEngine.RenderAsync("/Pages/OrderReceived.cshtml", order);

                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(invoiceHtml, Encoding.UTF8, "text/html")
                    };
                }
            }
            else
            {
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
            // return error
            return new HttpResponseMessage(HttpStatusCode.InternalServerError);
        }

        //[FunctionName("WelcomeEmail_ConsiderAction")]
        //public static async Task<HttpResponseMessage> ConsiderAction(
        //    [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req,
        //    [DurableClient] IDurableOrchestrationClient client,
        //    ILogger log)
        //{
        //    string instanceId = req.Query["instanceId"];

        //    await client.RaiseEventAsync(instanceId, "WelcomeEmail_Considered", true);


        //}
    }
}
