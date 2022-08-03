using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ftl.DigitalMarketing.AzureFunctions.Contract.Requests
{
    public class EmailLeadRequest
    {
        public string Email { get; set; }
        public string FormId { get; set; }
    }
}
