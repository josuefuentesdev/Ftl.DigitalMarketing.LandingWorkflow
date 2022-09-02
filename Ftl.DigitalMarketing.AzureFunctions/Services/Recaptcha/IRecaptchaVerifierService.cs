using System.Threading.Tasks;

namespace Ftl.DigitalMarketing.AzureFunctions.Services.Recaptcha
{
    public interface IRecaptchaVerifierService
    {
        public Task<bool> ValidatedRequest(string recaptchaResponse);
    }
}