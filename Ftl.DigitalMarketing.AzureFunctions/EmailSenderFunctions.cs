using FluentEmail.Core;
using Ftl.DigitalMarketing.AzureFunctions.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;
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

        [FunctionName("SendEmail")]
        public async Task<string> SendWelcomelEmail([ActivityTrigger] WorkflowOrchestratorRequest request, ILogger log)
        {
            //string emailPath = Path.Join(Directory.GetCurrentDirectory(), "Emails", "WelcomeEmail.cshtml");
            //var response = await _fluentEmail
            //        .To("josuefuentesdev@gmail.com")
            //        .Subject("prueba")
            //        .UsingTemplateFromFile(emailPath, request)
            //        .SendAsync();

            var template = @"
                Hi @Model.Name here is a list @foreach(var i in Model.Numbers) { @i }
            ";
            var model = new { Name = "LUKE", Numbers = new[] { "1", "2", "3" } };

            var response = await _fluentEmail
                .To("josuefuentesdev@gmail.com")
                .Subject("prueba")
                //.UsingTemplate(template, model)
                .SendAsync();

            return $"Hello {response.Successful} {response.MessageId}!";
            //return "hi";
        }
    }
}
