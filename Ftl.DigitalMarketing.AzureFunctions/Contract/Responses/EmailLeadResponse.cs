using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using System.Threading.Tasks;

namespace Ftl.DigitalMarketing.AzureFunctions.Contract.Responses
{
    public class EmailLeadResponse
    {
        [JsonProperty("success")]
        public bool Success { get; set; }
        [JsonProperty("message")]
        public string Message { get; set; }
        //public int ContactId { get; set; }
    }
}
