using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ftl.DigitalMarketing.AzureFunctions.Models
{
    public class ExpireOfferRequest
    {
        public int ContactId { get; set; }
        public string InstanceId { get; set; }
        public TimeSpan WaitFor { get; set; }
    }
}
