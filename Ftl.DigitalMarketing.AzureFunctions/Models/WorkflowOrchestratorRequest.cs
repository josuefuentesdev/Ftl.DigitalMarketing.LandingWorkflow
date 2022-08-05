using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ftl.DigitalMarketing.AzureFunctions.Models
{
    public class WorkflowOrchestratorRequest
    {
        public int ContactId { get; set; }
        public string Email { get; set; }
        public string InstanceId { get; set; }
    }
}
