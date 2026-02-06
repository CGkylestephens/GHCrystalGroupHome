using CrystalGroupHome.Internal.Features.EnvironmentComparer.Models;
using EpicorRestAPI;
using Newtonsoft.Json.Linq;
using System.IO.Compression;
using System.Text;

namespace CrystalGroupHome.Internal.Features.EnvironmentComparer.Data
{
    public interface IEpicorCustomizationService
    {
        Task<List<BaqDefinition>> GetBaqsAsync(string environmentName);
        Task<List<BpmDirectiveDefinition>> GetBpmDirectivesAsync(string environmentName);
        Task<List<UDColumnDTO>> GetUDColumnsAsync(string environmentName);
        Task<List<ApplicationLayerDefinition>> GetApplicationLayersAsync(string environmentName);
    }

    public class EpicorCustomizationService : IEpicorCustomizationService
    {
        private readonly IEpicorEnvironmentService _environmentService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<EpicorCustomizationService> _logger;

        public EpicorCustomizationService(
            IEpicorEnvironmentService environmentService, 
            IHttpClientFactory httpClientFactory,
            ILogger<EpicorCustomizationService> logger)
        {
            _environmentService = environmentService;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<List<BaqDefinition>> GetBaqsAsync(string environmentName)
        {
            // Create a temporary client without company context to fetch all BAQs
            var client = ((EpicorEnvironmentService)_environmentService).GetEpicorClient(environmentName, useCompany: true);
            var baqList = new List<BaqDefinition>();
            const int pageSize = 100;
            int skip = 0;
            bool moreRecords;

            try
            {
                do
                {
                    var parameters = new EpicorRestSharedClasses.MultiMap<string, string>
                    {
                        { "$select", "Company,QueryID,Description,IsShared,AuthorID,DisplayPhrase" },
                        { "$top", pageSize.ToString() },
                        { "$skip", skip.ToString() }
                    };

                    var response = await Task.Run(() => client.BoGet("Ice.BO.DynamicQuerySvc", "DynamicQueries", parameters));

                    if (response.IsErrorResponse || response.ResponseData?["value"] is not JArray baqs)
                    {
                        _logger.LogError("Failed to retrieve BAQs from {Environment}: {Error}", environmentName, response.ResponseError);
                        return baqList;
                    }

                    moreRecords = baqs.Count == pageSize;
                    skip += baqs.Count;

                    foreach (var baqNode in baqs)
                    {
                        if (baqNode == null) continue;

                        var baq = new BaqDefinition
                        {
                            Company = baqNode["Company"]?.Value<string>() ?? string.Empty,
                            QueryID = baqNode["QueryID"]?.Value<string>() ?? string.Empty,
                            Description = baqNode["Description"]?.Value<string>() ?? string.Empty,
                            IsShared = baqNode["IsShared"]?.Value<bool>() ?? false,
                            AuthorID = baqNode["AuthorID"]?.Value<string>() ?? string.Empty,
                            DisplayPhrase = baqNode["DisplayPhrase"]?.Value<string>() ?? string.Empty
                        };

                        baq.ComputeContentHash();
                        baqList.Add(baq);
                    }
                } while (moreRecords);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An exception occurred while fetching BAQs from {Environment}", environmentName);
            }

            return baqList;
        }

        public async Task<List<BpmDirectiveDefinition>> GetBpmDirectivesAsync(string environmentName)
        {
            var client = ((EpicorEnvironmentService)_environmentService).GetEpicorClient(environmentName, useCompany: true);
            var directiveList = new List<BpmDirectiveDefinition>();
            const int pageSize = 100;
            int skip = 0;
            bool moreRecords;

            try
            {
                do
                {
                    var parameters = new EpicorRestSharedClasses.MultiMap<string, string>
                    {
                        { "$top", pageSize.ToString() },
                        { "$skip", skip.ToString() }
                    };

                    var response = await Task.Run(() => client.BaqGet("CGI_CustomDirectives", parameters));

                    if (response.IsErrorResponse || response.ResponseData?["value"] is not JArray directives)
                    {
                        _logger.LogError("Failed to retrieve BPM Directives from {Environment}: {Error}", environmentName, response.ResponseError);
                        return directiveList;
                    }

                    moreRecords = directives.Count == pageSize;
                    skip += directives.Count;

                    foreach (var directiveNode in directives)
                    {
                        if (directiveNode == null) continue;

                        var directive = new BpmDirectiveDefinition
                        {
                            // BpDirective properties
                            Source = directiveNode["BpDirective_Source"]?.Value<string>() ?? string.Empty,
                            DirectiveType = directiveNode["BpDirective_DirectiveType"]?.Value<int>() ?? 0,
                            IsEnabled = directiveNode["BpDirective_IsEnabled"]?.Value<bool>() ?? false,
                            BpMethodCode = directiveNode["BpDirective_BpMethodCode"]?.Value<string>() ?? string.Empty,
                            Name = directiveNode["BpDirective_Name"]?.Value<string>() ?? string.Empty,
                            DirectiveID = directiveNode["BpDirective_DirectiveID"]?.Value<string>() ?? string.Empty,
                            Order = directiveNode["BpDirective_Order"]?.Value<int>() ?? 0,
                            DirectiveGroup = directiveNode["BpDirective_DirectiveGroup"]?.Value<string>() ?? string.Empty,
                            Description = directiveNode["BpDirective_Description"]?.Value<string>(),
                            Body = directiveNode["BpDirective_Body"]?.Value<string>() ?? string.Empty,

                            // BpMethod properties
                            MethodSource = directiveNode["BpMethod_Source"]?.Value<string>() ?? string.Empty,
                            SystemCode = directiveNode["BpMethod_SystemCode"]?.Value<string>() ?? string.Empty,
                            ObjectNS = directiveNode["BpMethod_ObjectNS"]?.Value<string>() ?? string.Empty,
                            BusinessObject = directiveNode["BpMethod_BusinessObject"]?.Value<string>() ?? string.Empty,
                            MethodName = directiveNode["BpMethod_Name"]?.Value<string>() ?? string.Empty,
                            MethodDescription = directiveNode["BpMethod_Description"]?.Value<string>(),
                            Version = directiveNode["BpMethod_Version"]?.Value<string>(),
                            HasRootTransaction = directiveNode["BpMethod_HasRootTransaction"]?.Value<bool>() ?? false,
                            DebugMode = directiveNode["BpMethod_DebugMode"]?.Value<bool>() ?? false,
                            DumpSources = directiveNode["BpMethod_DumpSources"]?.Value<bool>() ?? false,
                            AdvTracing = directiveNode["BpMethod_AdvTracing"]?.Value<bool>() ?? false
                        };

                        directive.ComputeContentHash();
                        directiveList.Add(directive);
                    }
                } while (moreRecords);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An exception occurred while fetching BPM Directives from {Environment}", environmentName);
            }

            return directiveList;
        }

        public async Task<List<UDColumnDTO>> GetUDColumnsAsync(string environmentName)
        {
            try
            {
                _logger.LogInformation("Fetching UD columns for environment: {EnvironmentName}", environmentName);

                var environment = _environmentService.GetConfiguredEnvironments()
                    .FirstOrDefault(e => e.Name.Equals(environmentName, StringComparison.OrdinalIgnoreCase));

                if (environment == null)
                {
                    throw new KeyNotFoundException($"Environment '{environmentName}' not found in configuration.");
                }

                // Use the named HttpClient with Windows Authentication
                using var httpClient = _httpClientFactory.CreateClient("WindowsAuth");
                
                // Ensure the BlazorDomain has a protocol scheme
                var baseUrl = environment.BlazorDomain.TrimEnd('/');
                if (!baseUrl.StartsWith("http://") && !baseUrl.StartsWith("https://"))
                {
                    baseUrl = $"https://{baseUrl}";
                }
                
                var apiUrl = $"{baseUrl}/api/epicompare/ud-columns";

                _logger.LogDebug("Making API call to: {ApiUrl}", apiUrl);

                var response = await httpClient.GetAsync(apiUrl);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("API call failed with status {StatusCode}: {ReasonPhrase}", 
                        response.StatusCode, response.ReasonPhrase);
                    throw new HttpRequestException($"API call failed: {response.StatusCode} - {response.ReasonPhrase}");
                }

                var jsonContent = await response.Content.ReadAsStringAsync();
                var options = new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var udColumns = System.Text.Json.JsonSerializer.Deserialize<List<UDColumnDTO>>(jsonContent, options) ?? new List<UDColumnDTO>();

                _logger.LogInformation("Successfully retrieved {Count} UD columns from {Environment}", 
                    udColumns.Count, environmentName);

                return udColumns;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching UD columns for environment: {EnvironmentName}", environmentName);
                throw;
            }
        }

        public async Task<List<ApplicationLayerDefinition>> GetApplicationLayersAsync(string environmentName)
        {
            var layerList = new List<ApplicationLayerDefinition>();
            var failedLayers = new List<string>();

            try
            {
                _logger.LogInformation("Fetching Application Studio applications for environment: {EnvironmentName}", environmentName);

                var environment = _environmentService.GetConfiguredEnvironments()
                    .FirstOrDefault(e => e.Name.Equals(environmentName, StringComparison.OrdinalIgnoreCase));

                if (environment == null)
                {
                    throw new KeyNotFoundException($"Environment '{environmentName}' not found in configuration.");
                }

                // Build the API base URL
                var baseUrl = $"https://{environment.AppPoolHost}/{environment.AppPoolInstance}/api/v1";

                // Get all applications with their layers using a single call with empty SubType
                var applications = await GetApplicationsAsync(environment, baseUrl);

                // Collect layers, filtering out SystemFlag layers (they cannot be exported)
                var allLayers = new List<ApplicationLayerDefinition>();
                int appsWithLayers = 0;
                int appsWithoutLayers = 0;
                int systemLayersSkipped = 0;

                foreach (var app in applications)
                {
                    if (app.Layers != null && app.Layers.Count > 0)
                    {
                        appsWithLayers++;
                        foreach (var layer in app.Layers)
                        {
                            // Skip system layers - they cannot be exported and will cause errors
                            if (layer.SystemFlag)
                            {
                                systemLayersSkipped++;
                                continue;
                            }
                            allLayers.Add(layer);
                        }
                    }
                    else
                    {
                        appsWithoutLayers++;
                    }
                }

                _logger.LogInformation(
                    "Retrieved {TotalApps} applications from {Environment}: {WithLayers} have custom layers ({LayerCount} exportable, {SystemSkipped} system layers skipped), {WithoutLayers} have no layers",
                    applications.Count, environmentName, appsWithLayers, allLayers.Count, systemLayersSkipped, appsWithoutLayers);

                if (allLayers.Count == 0)
                {
                    _logger.LogInformation("No exportable customization layers found in {Environment}", environmentName);
                    return layerList;
                }

                // Export layers in batches, with fallback to individual export on failure
                const int batchSize = 5;
                for (int i = 0; i < allLayers.Count; i += batchSize)
                {
                    var batch = allLayers.Skip(i).Take(batchSize).ToList();
                    var exportedLayers = await ExportLayersWithRetryAsync(environment, baseUrl, batch, failedLayers);
                    layerList.AddRange(exportedLayers);

                    // Log progress for large datasets
                    if ((i + batchSize) % 50 == 0 || i + batchSize >= allLayers.Count)
                    {
                        _logger.LogDebug("Export progress for {Environment}: {Processed}/{Total} layers processed, {Exported} exported, {Failed} failed", 
                            environmentName, Math.Min(i + batchSize, allLayers.Count), allLayers.Count, layerList.Count, failedLayers.Count);
                    }
                }

                if (failedLayers.Count > 0)
                {
                    _logger.LogWarning("Failed to export {FailedCount} layers from {Environment}: {FailedLayers}", 
                        failedLayers.Count, environmentName, string.Join(", ", failedLayers.Take(10)) + (failedLayers.Count > 10 ? "..." : ""));
                }

                _logger.LogInformation("Successfully exported {Count}/{Total} application layers from {Environment}", 
                    layerList.Count, allLayers.Count, environmentName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching application layers for environment: {EnvironmentName}", environmentName);
                throw;
            }

            return layerList;
        }

        private async Task<List<ApplicationDefinition>> GetApplicationsAsync(
            Common.Data._Epicor.EpicorRestSettings environment, 
            string baseUrl)
        {
            var applications = new List<ApplicationDefinition>();

            try
            {
                using var httpClient = CreateAuthenticatedHttpClient(environment);
                
                var requestUrl = $"{baseUrl}/Ice.LIB.MetaFXSvc/GetApplications";
                
                // Use empty SubType to get ALL applications, matching the native Kinetic behavior
                var payload = new
                {
                    request = new
                    {
                        Type = "view",
                        SubType = "",
                        SearchText = "",
                        IncludeAllLayers = true
                    }
                };

                var jsonContent = new StringContent(
                    System.Text.Json.JsonSerializer.Serialize(payload),
                    Encoding.UTF8,
                    "application/json");

                _logger.LogDebug("Calling GetApplications API at {Url}", requestUrl);

                var response = await httpClient.PostAsync(requestUrl, jsonContent);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("GetApplications failed with status {StatusCode}: {Error}", 
                        response.StatusCode, errorContent);
                    return applications;
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var responseObj = JObject.Parse(responseContent);
                var returnObj = responseObj["returnObj"] as JArray;

                if (returnObj == null)
                {
                    _logger.LogWarning("GetApplications returned null or invalid returnObj");
                    return applications;
                }

                _logger.LogDebug("GetApplications returned {Count} applications", returnObj.Count);

                foreach (var appNode in returnObj)
                {
                    var app = new ApplicationDefinition
                    {
                        Id = appNode["Id"]?.Value<string>() ?? string.Empty,
                        Type = appNode["Type"]?.Value<string>() ?? string.Empty,
                        SubType = appNode["SubType"]?.Value<string>() ?? string.Empty,
                        LastUpdated = appNode["LastUpdated"]?.Value<DateTime>() ?? DateTime.MinValue,
                        IsLayerDisabled = appNode["IsLayerDisabled"]?.Value<bool>() ?? false,
                        SystemFlag = appNode["SystemFlag"]?.Value<bool>() ?? false,
                        HasDraftContent = appNode["HasDraftContent"]?.Value<bool>() ?? false,
                        CreatedBy = appNode["CreatedBy"]?.Value<string>() ?? string.Empty,
                        CanAccessBase = appNode["CanAccessBase"]?.Value<bool>() ?? false,
                        SecurityCode = appNode["SecurityCode"]?.Value<string>() ?? string.Empty
                    };

                    // Parse layers if present - Layers array only exists if the app has custom layers
                    var layersNode = appNode["Layers"] as JArray;
                    if (layersNode != null && layersNode.Count > 0)
                    {
                        foreach (var layerNode in layersNode)
                        {
                            var layer = new ApplicationLayerDefinition
                            {
                                Id = layerNode["Id"]?.Value<string>() ?? string.Empty,
                                SubType = layerNode["SubType"]?.Value<string>() ?? string.Empty,
                                Type = app.Type,
                                TypeCode = layerNode["TypeCode"]?.Value<string>() ?? string.Empty,
                                LayerName = layerNode["LayerName"]?.Value<string>() ?? string.Empty,
                                DeviceType = layerNode["DeviceType"]?.Value<string>() ?? string.Empty,
                                Company = layerNode["Company"]?.Value<string>() ?? string.Empty,
                                CGCCode = layerNode["CGCCode"]?.Value<string>() ?? string.Empty,
                                LastUpdated = layerNode["LastUpdated"]?.Value<DateTime>() ?? DateTime.MinValue,
                                IsPublished = layerNode["IsPublished"]?.Value<bool>() ?? false,
                                SystemFlag = layerNode["SystemFlag"]?.Value<bool>() ?? false,
                                HasDraftContent = layerNode["HasDraftContent"]?.Value<bool>() ?? false,
                                LastUpdatedBy = layerNode["LastUpdatedBy"]?.Value<string>() ?? string.Empty,
                                IsLayerDisabled = layerNode["IsLayerDisabled"]?.Value<bool>() ?? false
                            };
                            app.Layers.Add(layer);
                        }
                    }

                    applications.Add(app);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling GetApplications API");
                throw;
            }

            return applications;
        }

        /// <summary>
        /// Exports layers with retry logic - if batch fails, tries individual exports
        /// </summary>
        private async Task<List<ApplicationLayerDefinition>> ExportLayersWithRetryAsync(
            Common.Data._Epicor.EpicorRestSettings environment,
            string baseUrl,
            List<ApplicationLayerDefinition> layers,
            List<string> failedLayers)
        {
            // First try batch export
            var exportedLayers = await ExportLayersAsync(environment, baseUrl, layers);
            
            // If batch succeeded or partially succeeded, return what we got
            if (exportedLayers.Count == layers.Count)
            {
                return exportedLayers;
            }

            // If batch completely failed, try exporting each layer individually
            if (exportedLayers.Count == 0 && layers.Count > 1)
            {
                _logger.LogDebug("Batch export failed, attempting individual layer exports for {Count} layers", layers.Count);
                
                foreach (var layer in layers)
                {
                    try
                    {
                        var singleLayerResult = await ExportLayersAsync(environment, baseUrl, [layer]);
                        if (singleLayerResult.Count > 0)
                        {
                            exportedLayers.AddRange(singleLayerResult);
                        }
                        else
                        {
                            failedLayers.Add($"{layer.Id}/{layer.LayerName}");
                            _logger.LogWarning("Failed to export layer: {AppId}/{LayerName}", layer.Id, layer.LayerName);
                        }
                    }
                    catch (Exception ex)
                    {
                        failedLayers.Add($"{layer.Id}/{layer.LayerName}");
                        _logger.LogWarning(ex, "Exception exporting layer: {AppId}/{LayerName}", layer.Id, layer.LayerName);
                    }
                }
            }
            else if (exportedLayers.Count < layers.Count)
            {
                // Some layers in the batch failed - identify which ones
                var exportedIds = exportedLayers.Select(l => $"{l.Id}|{l.LayerName}").ToHashSet();
                var missingLayers = layers.Where(l => !exportedIds.Contains($"{l.Id}|{l.LayerName}")).ToList();
                
                foreach (var missing in missingLayers)
                {
                    failedLayers.Add($"{missing.Id}/{missing.LayerName}");
                }
            }

            return exportedLayers;
        }

        private async Task<List<ApplicationLayerDefinition>> ExportLayersAsync(
            Common.Data._Epicor.EpicorRestSettings environment,
            string baseUrl,
            List<ApplicationLayerDefinition> layers)
        {
            var exportedLayers = new List<ApplicationLayerDefinition>();

            try
            {
                using var httpClient = CreateAuthenticatedHttpClient(environment);

                var requestUrl = $"{baseUrl}/Ice.LIB.MetaFXSvc/ExportLayers";

                // Build the apps payload matching the expected format
                var appsPayload = layers.Select(l => new
                {
                    l.Id,
                    l.SubType,
                    LastUpdated = l.LastUpdated.ToString("o"),
                    l.IsPublished,
                    l.TypeCode,
                    l.Company,
                    l.LayerName,
                    l.DeviceType,
                    l.CGCCode,
                    l.SystemFlag,
                    l.HasDraftContent,
                    l.LastUpdatedBy,
                    l.Type,
                    l.IsLayerDisabled,
                    UniqueId = $"{l.Id}{l.LayerName}{l.TypeCode}",
                    Status = 0
                }).ToArray();

                var payload = new { apps = appsPayload };

                var jsonContent = new StringContent(
                    System.Text.Json.JsonSerializer.Serialize(payload),
                    Encoding.UTF8,
                    "application/json");

                var response = await httpClient.PostAsync(requestUrl, jsonContent);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    // Only log at debug level for individual failures, we'll log summary at higher level
                    if (layers.Count == 1)
                    {
                        _logger.LogDebug("ExportLayers failed for {AppId}/{LayerName}: {StatusCode}", 
                            layers[0].Id, layers[0].LayerName, response.StatusCode);
                    }
                    else
                    {
                        _logger.LogDebug("ExportLayers batch failed with status {StatusCode} for {Count} layers", 
                            response.StatusCode, layers.Count);
                    }
                    return exportedLayers;
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var responseObj = JObject.Parse(responseContent);
                var base64Archive = responseObj["returnObj"]?.Value<string>();

                if (string.IsNullOrEmpty(base64Archive))
                {
                    _logger.LogDebug("ExportLayers returned empty archive for batch of {Count} layers", layers.Count);
                    return exportedLayers;
                }

                // Decode and extract the zip archive
                var archiveBytes = Convert.FromBase64String(base64Archive);
                using var archiveStream = new MemoryStream(archiveBytes);
                using var archive = new ZipArchive(archiveStream, ZipArchiveMode.Read);

                // Process each entry in the archive
                foreach (var entry in archive.Entries)
                {
                    if (entry.FullName.EndsWith(".jsonc", StringComparison.OrdinalIgnoreCase) ||
                        entry.FullName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    {
                        // Extract the layer name from the file path
                        // Format: Layers/{AppId}/{LayerName}.jsonc
                        var pathParts = entry.FullName.Split('/');
                        if (pathParts.Length >= 3)
                        {
                            var appId = pathParts[1];
                            var fileName = Path.GetFileNameWithoutExtension(pathParts[2]);

                            // Find the matching layer
                            var matchingLayer = layers.FirstOrDefault(l => 
                                l.Id.Equals(appId, StringComparison.OrdinalIgnoreCase) && 
                                l.LayerName.Equals(fileName, StringComparison.OrdinalIgnoreCase));

                            if (matchingLayer != null)
                            {
                                using var entryStream = entry.Open();
                                using var reader = new StreamReader(entryStream);
                                var content = await reader.ReadToEndAsync();

                                // Create a copy with the content
                                var exportedLayer = new ApplicationLayerDefinition
                                {
                                    Id = matchingLayer.Id,
                                    SubType = matchingLayer.SubType,
                                    Type = matchingLayer.Type,
                                    TypeCode = matchingLayer.TypeCode,
                                    LayerName = matchingLayer.LayerName,
                                    DeviceType = matchingLayer.DeviceType,
                                    Company = matchingLayer.Company,
                                    CGCCode = matchingLayer.CGCCode,
                                    LastUpdated = matchingLayer.LastUpdated,
                                    IsPublished = matchingLayer.IsPublished,
                                    SystemFlag = matchingLayer.SystemFlag,
                                    HasDraftContent = matchingLayer.HasDraftContent,
                                    LastUpdatedBy = matchingLayer.LastUpdatedBy,
                                    IsLayerDisabled = matchingLayer.IsLayerDisabled,
                                    LayerContent = content
                                };

                                // Extract the Content and SysCharacter03 fields for easier comparison
                                exportedLayer.ExtractContentFields();
                                exportedLayer.ComputeContentHash();
                                exportedLayers.Add(exportedLayer);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Exception in ExportLayersAsync for batch of {Count} layers", layers.Count);
            }

            return exportedLayers;
        }

        private HttpClient CreateAuthenticatedHttpClient(Common.Data._Epicor.EpicorRestSettings environment)
        {
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, sslPolicyErrors) => true
            };

            var httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromMinutes(5)
            };

            // Use API Key authentication if configured
            if (environment.UseApiKey && !string.IsNullOrEmpty(environment.APIKey))
            {
                httpClient.DefaultRequestHeaders.Add("x-api-key", environment.APIKey);
            }

            // Add basic auth
            var credentials = Convert.ToBase64String(
                Encoding.ASCII.GetBytes($"{environment.UserName}:{environment.Password}"));
            httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);

            return httpClient;
        }
    }
}