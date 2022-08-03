using FluentEmail.Core;
using Ftl.DigitalMarketing.AzureFunctions.Models;
using Ftl.DigitalMarketing.RazorTemplates;
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

        [FunctionName("SendEmail")]
        public async Task<string> SendWelcomelEmail([ActivityTrigger] WorkflowOrchestratorRequest request, ILogger log)
        {
            var invoiceModel = new Invoice
            {
                InvoiceNumber = "3232",
                CreatedDate = DateTime.Now,
                DueDate = DateTime.Now.AddDays(7),
                CompanyAddress = new Address
                {
                    Name = "XY Technologies",
                    AddressLine1 = "XY Street, Park Road",
                    City = "Chennai",
                    Country = "India",
                    Email = "xy-email@gmail.com",
                    PinCode = "600001"
                },
                BillingAddress = new Address
                {
                    Name = "XY Customer",
                    AddressLine1 = "ZY Street, Loyal Road",
                    City = "Bangalore",
                    Country = "India",
                    Email = "xy-customer@gmail.com",
                    PinCode = "343099"
                },
                PaymentMethod = new PaymentMethod
                {
                    Name = "Cheque",
                    ReferenceNumber = "94759849374"
                },
                LineItems = new List<LineItem>
                {
                    new LineItem
                    {
                    Id = 1,
                    ItemName = "USB Type-C Cable",
                    Quantity = 3,
                    PricePerItem = 10.33M
                    },
                       new LineItem
                    {
                    Id = 1,
                    ItemName = "SSD-512G",
                    Quantity = 10,
                    PricePerItem = 90.54M
                    }
                },
                        CompanyLogoUrl = "https://raw.githubusercontent.com/soundaranbu/RazorTemplating/master/src/Razor.Templating.Core/assets/icon.png"
                    };
                    var invoiceHtml = await RazorTemplateEngine.RenderAsync("WelcomeEmail.cshtml", invoiceModel);
                    Console.WriteLine(invoiceHtml);











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
        }
    }
}
