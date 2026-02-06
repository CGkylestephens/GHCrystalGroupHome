using CrystalGroupHome.Internal.Common.Data._Epicor;
using EpicorRestAPI;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace CrystalGroupHome.Internal.Features.EnvironmentComparer.Data
{
    public interface IEpicorEnvironmentService
    {
        List<EpicorRestSettings> GetConfiguredEnvironments();
        EpicorRest GetEpicorClient(string environmentName);
    }

    public class EpicorEnvironmentService : IEpicorEnvironmentService
    {
        private readonly List<EpicorRestSettings> _environments;
        private readonly ConcurrentDictionary<string, EpicorRest> _clientCache = new();

        public EpicorEnvironmentService(IOptions<List<EpicorRestSettings>> epicorEnvironmentsOptions)
        {
            _environments = epicorEnvironmentsOptions.Value ?? throw new InvalidOperationException("Epicor environments are not configured in appsettings.json");
        }

        public List<EpicorRestSettings> GetConfiguredEnvironments()
        {
            return _environments;
        }

        public EpicorRest GetEpicorClient(string environmentName)
        {
            return GetEpicorClient(environmentName, useCompany: true);
        }

        public EpicorRest GetEpicorClient(string environmentName, bool useCompany)
        {
            if (string.IsNullOrWhiteSpace(environmentName))
            {
                throw new ArgumentNullException(nameof(environmentName));
            }

            // Use a different cache key for company-less clients to avoid conflicts
            var cacheKey = useCompany ? environmentName : $"{environmentName}_NoCompany";

            // Return from cache if it exists
            if (_clientCache.TryGetValue(cacheKey, out var cachedClient))
            {
                return cachedClient;
            }

            var settings = _environments.FirstOrDefault(e => e.Name.Equals(environmentName, StringComparison.OrdinalIgnoreCase));

            if (settings == null)
            {
                throw new KeyNotFoundException($"Epicor environment with name '{environmentName}' not found in configuration.");
            }

            var newClient = new EpicorRest
            {
                AppPoolHost = settings.AppPoolHost,
                AppPoolInstance = settings.AppPoolInstance,
                UserName = settings.UserName,
                Password = settings.Password,
                APIKey = settings.APIKey,
                Company = useCompany ? settings.Company : string.Empty, // Conditionally set Company
                APIVersion = settings.APIVersion == "V2" ? EpicorRestVersion.V2 : EpicorRestVersion.V1
            };

            if (!newClient.CreateBearerToken())
            {
                throw new Exception($"Failed to authenticate to Epicor environment: {environmentName}");
            }

            _clientCache.TryAdd(cacheKey, newClient);

            return newClient;
        }
    }
}