using FluentEmail.MailKitSmtp;
using Ftl.DigitalMarketing.AzureFunctions.Services.Recaptcha;
using MailKit.Security;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

[assembly: FunctionsStartup(typeof(Ftl.DigitalMarketing.AzureFunctions.Startup))]

namespace Ftl.DigitalMarketing.AzureFunctions
{
    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            builder.Services.AddHttpClient();

            builder.Services.AddHttpClient<IRecaptchaVerifierService, RecaptchaVerifierService>(client =>
            {
                client.BaseAddress = new Uri("https://www.google.com/recaptcha/api/siteverify");
            });

            builder.Services
                .AddFluentEmail("fictitel@josuefuentesdev.com")
                .AddMailKitSender(new SmtpClientOptions
                {
                    Server = Environment.GetEnvironmentVariable("SmtpHost"),
                    Port = Int32.Parse(Environment.GetEnvironmentVariable("SmtpPort")),
                    Password = Environment.GetEnvironmentVariable("SmtpPass"),
                    UseSsl = true,
                    User = Environment.GetEnvironmentVariable("SmtpUser"),
                    SocketOptions = SecureSocketOptions.StartTls,
                    RequiresAuthentication = true,
                });
        }
    }
}
