using DurableTask.Core.Stats;
using Ftl.DigitalMarketing.ApiClientServices;
using Ftl.DigitalMarketing.AzureFunctions.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Ftl.DigitalMarketing.AzureFunctions.DurableEntities
{
    [JsonObject(MemberSerialization.OptIn)]
    public class ExecutionContact : IExecutionContact
    {
        [FunctionName(nameof(ExecutionContact))]
        public static Task Run(
        [EntityTrigger] IDurableEntityContext ctx,
        [DurableClient] IDurableClient client
        )
        => ctx.DispatchAsync<ExecutionContact>(client);
        
        [JsonIgnore]
        private readonly HttpClient client;
        [JsonIgnore]
        private BackofficeApiClient _backofficeClient;
        [JsonIgnore]
        private readonly IDurableClient _durableClient;
        
        public ExecutionContact(IHttpClientFactory factory, IDurableClient durableClient)
        {
            client = factory.CreateClient();
            _backofficeClient = new("https://localhost:5001", client);
            this._durableClient = durableClient;
        }

        public int ContactId { get; set; }

        public string Stage { get; set; }

        public void SetContactId(int ContactId)
        {
            this.ContactId = ContactId;
            ContactEventData contactEventData = new();
            contactEventData.EventType = "CONTACT CREATED";
            Notify(contactEventData);
        }

        public void Notify(ContactEventData contactEventData)
        {
            var _ = _backofficeClient.CreateContactEventAsync(new CreateContactEventDto()
            {
                ContactId = ContactId,
                EventType = contactEventData.EventType
            });
        }

        public void NotifyEventType(string EventType)
        {
            var _ = _backofficeClient.CreateContactEventAsync(new CreateContactEventDto()
            {
                ContactId = ContactId,
                EventType = EventType
            });
        }

        [FunctionName("UpdateStage")]
        public async Task UpdateStage(
            string stage
            )
        {
            Stage = stage;
            await _durableClient.RaiseEventAsync(Entity.Current.EntityId.EntityKey, stage, true);
        }


    };
}
