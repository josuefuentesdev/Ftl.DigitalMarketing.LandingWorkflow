using Google.Protobuf.WellKnownTypes;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Ftl.DigitalMarketing.AzureFunctions.Services.Recaptcha
{
    public class RecaptchaVerifierService : IRecaptchaVerifierService
    {
        private readonly HttpClient _httpClient;
        private readonly string _remoteServiceBaseUrl;
        private string _recaptchaSecret;

        public RecaptchaVerifierService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _recaptchaSecret = Environment.GetEnvironmentVariable("GoogleRecaptchaV3Secret");
        }

        public async Task<bool> ValidatedRequest(string recaptchaResponse)
        {
            var data = new Dictionary<string, string>
            {
                {"secret", _recaptchaSecret},
                {"response", recaptchaResponse}
            };

            var res = await _httpClient.PostAsync(_remoteServiceBaseUrl, new FormUrlEncodedContent(data));

            var content = await res.Content.ReadAsStringAsync();

            var response = JsonConvert.DeserializeObject<RecaptchaResponse>(content);

            if (!response.success) return false;
            if (response.score < 0.3) return false;
            return true;
        }
    }
}
