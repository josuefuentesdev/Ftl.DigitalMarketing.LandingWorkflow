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
        string token;

        public EmailSenderFunctions(IFluentEmail fluentEmail, HttpClient http)
        {
            _fluentEmail = fluentEmail;
            _http = http;
            _backofficeClient = new(Environment.GetEnvironmentVariable("BACKOFFICE_URL"), _http);
            token = Environment.GetEnvironmentVariable("CONTACT_TOKEN");
        }

        [FunctionName("EmailSender_WelcomeEmail")]
        public async Task SendWelcomelEmail([ActivityTrigger] ExecutionContactRequest request, ILogger log)
        {
            var contact = await _backofficeClient.GetContactByIdAsync(request.ContactId, token);
            if (contact == null) return;


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
        }

        [FunctionName("EmailSender_ExpiredOffer")]
        public async Task SendExpiredOffer([ActivityTrigger] int contactId, ILogger log)
        {
            var contact = await _backofficeClient.GetContactByIdAsync(contactId, token);
            if (contact == null) return;


            var invoiceHtml = await RazorTemplateEngine.RenderAsync("/Emails/ExpiredEmail.cshtml");

            var response = await _fluentEmail
                .To(contact.Email)
                .Subject("Expired Offer :(")
                .Body(invoiceHtml, true)
                .SendAsync();
        }

        
        [FunctionName("EmailSender_OrderDetailsEmail")]
        public async Task<bool> SendOrderDetailsEmail([ActivityTrigger] OrderDetailsEmailModel request, ILogger log)
        {
            var contact = await _backofficeClient.GetContactByIdAsync(request.ContactId, token);
            if (contact == null) return false;
            
            OrderDetailsEmailModel orderEmailModel = new();
            orderEmailModel.UnsuscribeUrl = $"http://{websiteHostname}/api/UnsubscribeAction?instanceId={request.InstanceId}";
            orderEmailModel.OrderId = request.OrderId;

            var invoiceHtml = await RazorTemplateEngine.RenderAsync("/Emails/OrderDetailsEmail.cshtml", orderEmailModel);

            var response = await _fluentEmail
                .To(contact.Email)
                .Subject("Your order was confirmed")
                .Body(invoiceHtml, true)
                .SendAsync();

            return response.Successful;
        }

        [FunctionName("EmailSender_RemainderEmail")]
        public async Task SendRemainderEmail([ActivityTrigger] ExecutionContactRequest request, ILogger log)
        {
            var contact = await _backofficeClient.GetContactByIdAsync(request.ContactId, token);
            if (contact == null) return;

            RemainderEmailModel remainderEmailModel = new();
            remainderEmailModel.UnsuscribeUrl = $"http://{websiteHostname}/api/UnsubscribeAction?instanceId={request.InstanceId}";
            remainderEmailModel.BuyActionUrl = $"http://{websiteHostname}/api/WelcomeEmail_BuyAction?instanceId={request.InstanceId}";
            remainderEmailModel.BrandUrl = "https://josuefuentesdev.com";
            
            var invoiceHtml = await RazorTemplateEngine.RenderAsync("/Emails/RemainderEmail.cshtml", remainderEmailModel);

            var response = await _fluentEmail
                .To(contact.Email)
                .Subject("You are very close, to be blazingly fast...")
                .Body(invoiceHtml, true)
                .SendAsync();

        }
    }
}
