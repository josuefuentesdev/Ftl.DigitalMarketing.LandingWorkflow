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
        
        private readonly HttpClient _client;
        private BackofficeApiClient _backofficeClient;
        private readonly IDurableClient _durableClient;
        
        public ExecutionContact(IHttpClientFactory factory, IDurableClient durableClient)
        {
            _client = factory?.CreateClient();
            if (_client != null) _backofficeClient = new("https://localhost:5001", _client);
            _durableClient = durableClient;
        }

        [JsonProperty("contactId")]
        public int ContactId { get; set; }

        [JsonProperty("stage")]
        public string Stage { get; set; } = "START";

        [JsonProperty("orderId")]
        public int OrderId { get; set; }

        [JsonProperty("expiredOffer")]
        public bool ExpiredOffer { get; set; }

        public void SetContactId(int ContactId)
        {
            this.ContactId = ContactId;
            
            NotifyEventType("CONTACT CREATED");

            ExecutionContactRequest executionContactRequest = new()
            {
                ContactId = ContactId,
                InstanceId = Entity.Current.EntityId.EntityKey
            };
            Entity.Current.StartNewOrchestration("WelcomeOrchestrator", executionContactRequest);

            // expire the offer after some time
            ExpireOfferRequest expireRequest = new()
            {
                ContactId = ContactId,
                InstanceId = Entity.Current.EntityId.EntityKey,
                WaitFor = TimeSpan.FromMinutes(5)
            };
            Entity.Current.StartNewOrchestration("ExpiredOfferOrchestrator", expireRequest);
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
            if (Stage != "DECISION" && Stage != stage && !ExpiredOffer)
            {
                Stage = stage;
                if (stage == "DECISION")
                {
                    // create order
                    CreateOrderDto createOrderDto = new()
                    {
                        ContactId = ContactId,
                        Status = "PENDING"
                    };
                    OrderId = await _backofficeClient.CreateOrderAsync(createOrderDto);
                    // start decision orchestration 
                    ExecutionContactRequest executionContactRequest = new()
                    {
                        ContactId = ContactId,
                        InstanceId = Entity.Current.EntityId.EntityKey
                    };
                    Entity.Current.StartNewOrchestration("DecisionOrchestrator", executionContactRequest);
                }
                else if (stage == "CONSIDER")
                {
                    // create decision orchestration
                }
            }
        }

        public async Task<int> GetOrderId()
        {
            return OrderId;
        }
    }
}
