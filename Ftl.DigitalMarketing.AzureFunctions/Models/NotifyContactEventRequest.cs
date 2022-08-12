using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ftl.DigitalMarketing.AzureFunctions.Models
{
    public class NotifyContactEventRequest
    {
        public string InstanceId { get; set; }
        public string EventType { get; set; }
    }
}
