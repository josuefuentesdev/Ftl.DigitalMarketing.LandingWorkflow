using Ftl.DigitalMarketing.AzureFunctions.Models;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ftl.DigitalMarketing.AzureFunctions.DurableEntities
{
    public interface IExecutionContact
    {
        public void SetContactId(int ContactId);
        public void Notify(ContactEventData contactEventData);
        public void NotifyEventType(string EventType);
        public Task UpdateStage(string stage);
        public Task<int> GetOrderId();
    }
}
