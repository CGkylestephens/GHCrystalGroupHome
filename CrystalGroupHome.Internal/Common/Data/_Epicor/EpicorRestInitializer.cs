using EpicorRestAPI;
using Microsoft.Extensions.Options;

namespace CrystalGroupHome.Internal.Common.Data._Epicor
{
    public class EpicorRestInitializer
    {
        private readonly EpicorRestSettings _settings;

        public EpicorRest Client { get; }

        public EpicorRestInitializer(IOptions<EpicorRestSettings> options)
        {
            _settings = options.Value;
            Client = new EpicorRest
            {
                AppPoolHost = _settings.AppPoolHost,
                AppPoolInstance = _settings.AppPoolInstance,
                UserName = _settings.UserName,
                Password = _settings.Password,
                APIKey = _settings.APIKey,
                Company = _settings.Company,
                APIVersion = _settings.APIVersion == "V2" ? EpicorRestVersion.V2 : EpicorRestVersion.V1
            };

            if (!Client.CreateBearerToken())
            {
                throw new Exception("Failed to authenticate to Epicor");
            }
        }
    }

}
