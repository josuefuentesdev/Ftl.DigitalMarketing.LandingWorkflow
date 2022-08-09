using FluentEmail.Core;
using Ftl.DigitalMarketing.ApiClientServices;
using Ftl.DigitalMarketing.AzureFunctions.Models;
using Ftl.DigitalMarketing.RazorTemplates;
using Ftl.DigitalMarketing.RazorTemplates.Emails;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;
using Razor.Templating.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Ftl.DigitalMarketing.AzureFunctions
{
    public class EmailSenderFunctions
    {
        private IFluentEmail _fluentEmail;
        private readonly HttpClient _http;
        private BackofficeApiClient _backofficeClient;
        string websiteHostname;

        public EmailSenderFunctions(IFluentEmail fluentEmail, HttpClient http)
        {
            _fluentEmail = fluentEmail;
            _http = http;
            _backofficeClient = new("https://localhost:5001", _http);
            websiteHostname = Environment.GetEnvironmentVariable("WEBSITE_HOSTNAME");
        }

        [FunctionName("EmailSender_WelcomeEmail")]
        public async Task<string> SendWelcomelEmail([ActivityTrigger] ExecutionContactRequest request, ILogger log)
        {
            var contact = await _backofficeClient.GetContactByIdAsync(request.ContactId);
            if (contact == null) return "ERROR-INVALID-CONTACT";


            WelcomeEmailModel welcomeEmailModel = new();
            welcomeEmailModel.BuyActionUrl = $"http://{websiteHostname}/api/WelcomeEmail_BuyAction?instanceId={request.InstanceId}";
            welcomeEmailModel.AlternativeActionUrl = $"http://{websiteHostname}/api/WelcomeEmail_ConsiderAction?instanceId={request.InstanceId}";
            welcomeEmailModel.BrandUrl = "https://josuefuentesdev.com";
            welcomeEmailModel.UnsuscribeUrl = $"http://{websiteHostname}/api/UnsubscribeAction?instanceId={request.InstanceId}";

            var invoiceHtml = await RazorTemplateEngine.RenderAsync("/Emails/WelcomeEmail.cshtml", welcomeEmailModel);

            var response = await _fluentEmail
                .To(contact.Email)
                .Subject("Welcome to Fictitel")
                .Body(invoiceHtml, true)
                .SendAsync();

            return $"Hello {response.Successful} {response.MessageId}!";
        }


        [FunctionName("EmailSender_OrderDetailsEmail")]
        public async Task<string> SendOrderDetailsEmail([ActivityTrigger] ExecutionContactRequest request, ILogger log)
        {
            var contact = await _backofficeClient.GetContactByIdAsync(request.ContactId);
            if (contact == null) return "ERROR-INVALID-CONTACT";
            
            OrderDetailsEmailModel orderEmailModel = new();
            orderEmailModel.UnsuscribeUrl = $"http://{websiteHostname}/api/UnsubscribeAction?instanceId={request.InstanceId}";

            var invoiceHtml = await RazorTemplateEngine.RenderAsync("/Emails/OrderDetailsEmail.cshtml", orderEmailModel);

            var response = await _fluentEmail
                .To(contact.Email)
                .Subject("Your order was confirmed")
                .Body(invoiceHtml, true)
                .SendAsync();

            return $"Hello {response.Successful} {response.MessageId}!";
        }

        [FunctionName("EmailSender_RemainderEmail")]
        public async Task<string> SendRemainderEmail([ActivityTrigger] ExecutionContactRequest request, ILogger log)
        {
            var contact = await _backofficeClient.GetContactByIdAsync(request.ContactId);
            if (contact == null) return "ERROR-INVALID-CONTACT";

            RemainderEmailModel remainderEmailModel = new();
            remainderEmailModel.UnsuscribeUrl = $"http://{websiteHostname}/api/UnsubscribeAction?instanceId={request.InstanceId}";

            var invoiceHtml = await RazorTemplateEngine.RenderAsync("/Emails/RemainderEmail.cshtml", remainderEmailModel);

            var response = await _fluentEmail
                .To(contact.Email)
                .Subject("Your order was confirmed")
                .Body(invoiceHtml, true)
                .SendAsync();

            return $"Hello {response.Successful} {response.MessageId}!";
        }
    }
}
