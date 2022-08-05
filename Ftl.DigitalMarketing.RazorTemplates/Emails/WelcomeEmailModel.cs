using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ftl.DigitalMarketing.RazorTemplates.Emails
{
    public class WelcomeEmailModel
    {
        public string BuyActionUrl { get; set; }
        public string AlternativeActionUrl { get; set; }
        public string UnsuscribeUrl { get; set; }
        public string BrandUrl { get; set; }
    }
}
