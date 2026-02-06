using Blazorise;
using Blazorise.Bootstrap5;
using Blazorise.Icons.FontAwesome;
using CrystalGroupHome.Internal.Authorization;
using CrystalGroupHome.Internal.Authorization.Handlers;
using CrystalGroupHome.Internal.Authorization.Requirements;
using CrystalGroupHome.Internal.Common.Data._Epicor;
using CrystalGroupHome.Internal.Common.Data.Customers;
using CrystalGroupHome.Internal.Common.Data.Jobs;
using CrystalGroupHome.Internal.Common.Data.Labor;
using CrystalGroupHome.Internal.Common.Data.Parts;
using CrystalGroupHome.Internal.Common.Data.Vendors;
using CrystalGroupHome.Internal.Common.Helpers;
using CrystalGroupHome.Internal.Features.CMHub.CMDex.Data;
using CrystalGroupHome.Internal.Features.CMHub.CMNotif.Data;
using CrystalGroupHome.Internal.Features.CMHub.CustComms.Data;
using CrystalGroupHome.Internal.Features.CMHub.VendorComms.Data;
using CrystalGroupHome.Internal.Features.EnvironmentComparer.Data;
using CrystalGroupHome.Internal.Features.FirstTimeYield.Data;
using CrystalGroupHome.Internal.Features.ProductFailures.Data;
using CrystalGroupHome.Internal.Features.RMAProcessing.Data;
using CrystalGroupHome.Internal.Features.RMAProcessing.Models;
using CrystalGroupHome.SharedRCL.Data;
using CrystalGroupHome.SharedRCL.Helpers;
using CrystalGroupHome.SharedRCL.Services;
using DiffPlex;
using DiffPlex.DiffBuilder;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseStaticWebAssets();

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddControllers();
builder.Services.AddHttpClient();

// Add named HttpClient with Windows Authentication
builder.Services.AddHttpClient("WindowsAuth", client =>
{
    // Configure any default settings here
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler()
{
    UseDefaultCredentials = true
});

// Add Windows Authentication
builder.Services.AddAuthentication(NegotiateDefaults.AuthenticationScheme)
   .AddNegotiate();

// Configure Authorization Policies
builder.Services.AddAuthorization(options =>
{
	options.FallbackPolicy = options.DefaultPolicy;

	// IT Access Policy
	options.AddPolicy(AuthorizationPolicies.ITAccess, policy =>
		policy.Requirements.Add(new ITAccessRequirement()));

	// RMA Processing Policy
	options.AddPolicy(AuthorizationPolicies.RMAProcessingAccess, policy =>
		policy.Requirements.Add(new RMAProcessingAccessRequirement()));

	// First Time Yield Policies
	options.AddPolicy(AuthorizationPolicies.FirstTimeYieldAdmin, policy =>
		policy.Requirements.Add(new FirstTimeYieldAdminRequirement()));

	// CM Hub Policies
	options.AddPolicy(AuthorizationPolicies.CMHubAdmin, policy =>
		policy.Requirements.Add(new CMHubAdminRequirement()));

	options.AddPolicy(AuthorizationPolicies.CMHubVendorCommsEdit, policy =>
		policy.Requirements.Add(new CMHubVendorCommsEditRequirement()));

	options.AddPolicy(AuthorizationPolicies.CMHubCustCommsEdit, policy =>
		policy.Requirements.Add(new CMHubCustCommsEditRequirement()));

	options.AddPolicy(AuthorizationPolicies.CMHubCustCommsTaskStatusEdit, policy =>
		policy.Requirements.Add(new CMHubCustCommsTaskStatusEditRequirement()));

	options.AddPolicy(AuthorizationPolicies.CMHubCMDexEdit, policy =>
		policy.Requirements.Add(new CMHubCMDexEditRequirement()));

	options.AddPolicy(AuthorizationPolicies.CMHubCMNotifDocumentEdit, policy =>
		policy.Requirements.Add(new CMHubCMNotifDocumentEditRequirement()));

	options.AddPolicy(AuthorizationPolicies.CMHubCMNotifCreateLog, policy =>
		policy.Requirements.Add(new CMHubCMNotifCreateLogRequirement()));

	options.AddPolicy(AuthorizationPolicies.CMHubTechServicesEdit, policy =>
		policy.Requirements.Add(new CMHubTechServicesEditRequirement()));
});

// Register Authorization Handlers
builder.Services.AddSingleton<IAuthorizationHandler, ITAccessHandler>();
builder.Services.AddSingleton<IAuthorizationHandler, RMAProcessingAccessHandler>();
builder.Services.AddSingleton<IAuthorizationHandler, FirstTimeYieldAdminHandler>();
builder.Services.AddSingleton<IAuthorizationHandler, CMHubAdminHandler>();
builder.Services.AddSingleton<IAuthorizationHandler, CMHubVendorCommsEditHandler>();
builder.Services.AddSingleton<IAuthorizationHandler, CMHubCustCommsEditHandler>();
builder.Services.AddSingleton<IAuthorizationHandler, CMHubCustCommsTaskStatusEditHandler>();
builder.Services.AddSingleton<IAuthorizationHandler, CMHubCMDexEditHandler>();
builder.Services.AddSingleton<IAuthorizationHandler, CMHubCMNotifDocumentEditHandler>();
builder.Services.AddSingleton<IAuthorizationHandler, CMHubCMNotifCreateLogHandler>();
builder.Services.AddSingleton<IAuthorizationHandler, CMHubTechServicesEditHandler>();

// Configure Blazorise
builder.Services
	.AddBlazorise(options =>
	{
		options.ProductToken = "CjxRBXB/Nww9VAVyfjA1BlEAc3g1CzxQAXR+Ngs+bjoNJ2ZdYhBVCCo/CjlVA0xERldhE1EvN0xcNm46FD1gSkUHCkxESVFvBl4yK1FBfAYKEytiTWACQkxEWmdIImQACVdxSDxvDA9dZ1MxfxYdWmc2LX8eAkx1RTdjTERaZ002ZA4NSnVcL3UVC1pnQSJoHhFXd1swbx50S3dTL3kMB1FrAWlvHg1NeV43Yx4RSHlUPG8TAVJrUzwKDwFadEUueRUdCDJTPHwIHVFuRSZnHhFIeVQ8bxMBUmtTPAoPAVp0RS55FR0IMlM8ZBMLQG5FJmceEUh5VDxvEwFSa1M8Cg8BWnRFLnkVHQgySQtSBww3ck82XSArLnR5UWoxejRXNEh9BiFhVWgNXyA9Qg44IUR5D2NaOQcJAhtWCUILeWocV0JfKQEkOWpfbQ0fDTdWXHstcRF/NAk+J1kUfDV7fVtAIzloQX8nXmp5MnZfFVwPPkJoXS5qcQFBV05aUggIRmtdU3QxNkFCai4CFxZTTm0yY24LbghAKns0D2hOOjFjdikuVUYlWnV9N0A1AFkCJn8LQzBlfA==";
		options.Immediate = true;
	})
	.AddBootstrap5Providers()
	.AddFontAwesomeIcons();

builder.Services.Configure<EpicorRestSettings>(builder.Configuration.GetSection("EpicorRestSettings"));
builder.Services.Configure<List<EpicorRestSettings>>(builder.Configuration.GetSection("EpicorEnvironments"));
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("EmailOptions"));
builder.Services.Configure<VendorSurveyFeatureOptions>(builder.Configuration.GetSection("VendorSurveys"));
builder.Services.Configure<CMNotificationsFeatureOptions>(builder.Configuration.GetSection("CMNotifications"));
builder.Services.Configure<DatabaseOptions>(options =>
{
    options.CgiConnection = builder.Configuration.GetConnectionString("CgiConnection")
        ?? throw new InvalidOperationException("Connection string 'CgiConnection' not found.");
    options.CGIExtConnection = builder.Configuration.GetConnectionString("CGIExtConnection")
        ?? throw new InvalidOperationException("Connection string 'CGIExtConnection' not found.");
    options.KineticErpConnection = builder.Configuration.GetConnectionString("KineticErpConnection")
        ?? throw new InvalidOperationException("Connection string 'KineticErpConnection' not found.");
});
builder.Services.Configure<RMAProcessingOptions>(builder.Configuration.GetSection("RMAProcessing"));

builder.Services.AddScoped<ISideBySideDiffBuilder, SideBySideDiffBuilder>();
builder.Services.AddScoped<IDiffer, Differ>();

builder.Services.AddScoped<IEpicorEnvironmentService, EpicorEnvironmentService>();
builder.Services.AddScoped<IEpicorCustomizationService, EpicorCustomizationService>();
builder.Services.AddScoped<IEnvironmentComparisonService, EnvironmentComparisonService>();
builder.Services.AddScoped<IEpicorPartService, EpicorPartService>();
builder.Services.AddScoped<IProductFailureService, ProductFailureService>();
builder.Services.AddScoped<IJobService, JobService>();
builder.Services.AddScoped<ILaborService, LaborService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IVendorService, VendorService>();
builder.Services.AddScoped<IADUserService, ADUserService>();
builder.Services.AddScoped<IFirstTimeYield_Service, FirstTimeYield_Service>();
builder.Services.AddScoped<IPartService, PartService>();
builder.Services.AddScoped<ICMHub_CMDexService, CMHub_CMDexService>();
builder.Services.AddScoped<ICMHub_VendorCommsService, CMHub_VendorCommsService>();
builder.Services.AddScoped<ICMHub_VendorCommsSurveyService, CMHub_VendorCommsSurveyService>();
builder.Services.AddScoped<ICMHub_CustCommsService, CMHub_CustCommsService>();
builder.Services.AddScoped<ICMHub_CMNotifService, CMHub_CMNotifService>();

// RMA File Services - Register individual services first, then the main orchestrator
builder.Services.AddScoped<IRMAFileStorageService, RMAFileStorageService>();
builder.Services.AddScoped<IRMAFileDataService, RMAFileDataService>();
builder.Services.AddScoped<IRMAFileCategoryService, RMAFileCategoryService>();
builder.Services.AddScoped<IRMAFileProcessingService, RMAFileProcessingService>();
builder.Services.AddScoped<IRMAValidationService, RMAValidationService>();
builder.Services.AddScoped<IRMAFileService, RMAFileService>(); // Main orchestrator service

builder.Services.AddScoped<JsConsole>();
builder.Services.AddScoped<EnvironmentHelpers>();
builder.Services.AddScoped<EmailHelpers>();
builder.Services.AddScoped<ECNHelpers>();

// Impersonation Service - Scoped so each user session gets their own instance
builder.Services.AddScoped<ImpersonationService>();
builder.Services.AddScoped<DebugModeService>();
builder.Services.AddScoped<ITempFileService, TempFileService>();

builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
builder.Services.AddSingleton<EpicorRestInitializer>();
builder.Services.AddSingleton<IntranetConfig>(sp =>
{
	var configuration = sp.GetRequiredService<IConfiguration>();
	return new IntranetConfig
	{
		LegacyIntranetHostName = configuration["InternalLinks:LegacyIntranetHostName"] ?? "intranet"
	};
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
	app.UseExceptionHandler("/Error");
	app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host"); 

app.Run();