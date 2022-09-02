using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Ftl.DigitalMarketing.AzureFunctions.Services.Recaptcha
{
    public class RecaptchaResponse
    {
        public bool success { get; set; }
        public DateTime? challenge_ts { get; set; }
        public string? hostname { get; set; }
        public float? score { get; set; }
        public string? action { get; set; }
        [JsonPropertyName("error-codes")]
        public string[]? error_codes { get; set; }
    }
}
