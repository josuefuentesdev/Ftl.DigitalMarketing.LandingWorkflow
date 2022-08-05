using FluentEmail.Core;
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
using System.Text;
using System.Threading.Tasks;

namespace Ftl.DigitalMarketing.AzureFunctions
{
    public class EmailSenderFunctions
    {
        private IFluentEmail _fluentEmail;

        public EmailSenderFunctions(IFluentEmail fluentEmail)
        {
            _fluentEmail = fluentEmail;
        }

        [FunctionName("EmailSender_WelcomeEmail")]
        public async Task<string> SendWelcomelEmail([ActivityTrigger] WorkflowOrchestratorRequest request, ILogger log)
        {
            string websiteHostname = Environment.GetEnvironmentVariable("WEBSITE_HOSTNAME");
            WelcomeEmailModel welcomeEmailModel = new();
            welcomeEmailModel.BuyActionUrl = $"http://{websiteHostname}/api/WelcomeEmail_BuyAction?instanceId={request.InstanceId}";
            welcomeEmailModel.AlternativeActionUrl = "";
            welcomeEmailModel.BrandUrl = "https://josuefuentesdev.com";
            welcomeEmailModel.UnsuscribeUrl = "";
            
            var invoiceHtml = await RazorTemplateEngine.RenderAsync("/Emails/WelcomeEmail.cshtml", welcomeEmailModel);
            Console.WriteLine(invoiceHtml);

            var response = await _fluentEmail
                .To("josuefuentesdev@gmail.com")
                .Subject("prueba")
                .Body(invoiceHtml, true)
                .SendAsync();

            return $"Hello {response.Successful} {response.MessageId}!";
        }


        [FunctionName("EmailSender_OrderDetailsEmail")]
        public async Task<string> SendOrderDetailsEmail([ActivityTrigger] WorkflowOrchestratorRequest request, ILogger log)
        {
            string websiteHostname = Environment.GetEnvironmentVariable("WEBSITE_HOSTNAME");
            WelcomeEmailModel welcomeEmailModel = new();
            welcomeEmailModel.BuyActionUrl = $"http://{websiteHostname}/api/WelcomeEmail_BuyAction?instanceId={request.InstanceId}";
            welcomeEmailModel.AlternativeActionUrl = "";
            welcomeEmailModel.BrandUrl = "https://josuefuentesdev.com";
            welcomeEmailModel.UnsuscribeUrl = "";

            var invoiceHtml = await RazorTemplateEngine.RenderAsync("/Emails/OrderDetailsEmail.cshtml", welcomeEmailModel);
            Console.WriteLine(invoiceHtml);

            var response = await _fluentEmail
                .To("josuefuentesdev@gmail.com")
                .Subject("prueba")
                .Body(invoiceHtml, true)
                .SendAsync();

            return $"Hello {response.Successful} {response.MessageId}!";
        }
    }
}
