using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ftl.DigitalMarketing.RazorTemplates.Emails
{
    public class OrderDetailsEmailModel
    {
        public string UnsuscribeUrl { get; set; }
        public string InstanceId { get; set; }
        public int ContactId { get; set; }
        public int OrderId { get; set; }
    }
}
