using Blazorise;
using Blazorise.Bootstrap5;
using Blazorise.Icons.FontAwesome;
using CrystalGroupHome.External.Features.VendorSurvey.Data;
using CrystalGroupHome.External.Middleware;
using CrystalGroupHome.SharedRCL.Data;
using CrystalGroupHome.SharedRCL.Helpers;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseStaticWebAssets();

// Note: Server header suppression is now handled at the IIS level

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// Configure Blazorise
builder.Services
    .AddBlazorise(options =>
    {
        options.ProductToken = "CjxRBXF9NAg9VgJzfjM1BlEAc3g1CzxQAXR+Ngs+bjoNJ2ZdYhBVCCo/CjlVA0xERldhE1EvN0xcNlEIcUMPbX8GQggqP3w+VHUCDUcLISEJBwoofUpSAWwPN3k7TnIHfjEINVZ1B3ZBDgFpfCgtYFZ/BmQ4PmACXgZXNCJkSgFpbx4KRGxNJGIIClpnQSJoHhFXd1swbx50S3dTL3kMB1FrAWlvHg9QbEMgfwweSX1YJm8eA0RgUzxiDhlWZ1NZfg4RSXFBKmQSQw9nUyB4ABxRa1M8fQAWWmdeLGcSEVoCQixvDQdIcVgwPUsRWnRFMGQXB0BvUzx9ABZaZ14sZxIRWgJCLG8NB0hxWDA9SxFabF4mdRcHQG9TPH0AFlpnXixnEhFaAkIsbw0HSHFYMD1LKWtvQDUCDz1AbmZRCQwlNnpaUHQFG0xRajBYIB00fzgRHwsLfGFoDgIGGWhORVtaCQ1sUGgyCQAWZA1fNwkIPGFZWg1BFQBLbEpQVAcmN29rDEkFHipJZVFiIwljcG8EGzU4NlluDlokfi5KejtTLDpBaVgnAAAEc0xVFXIVDEx1WCcGcR91d2g0CSsGS3JtDnc1PV9TdRF4JBtVDE4ZaWoqRndFEwYnGlF5MQ==";
        options.Immediate = true;
    })
    .AddBootstrap5Providers()
    .AddFontAwesomeIcons();

// Configure shared services
builder.Services.Configure<DatabaseOptions>(builder.Configuration.GetSection("ConnectionStrings"));

// Add shared services
builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
builder.Services.AddScoped<IVendorSurveyService, VendorSurveyService>();
builder.Services.AddScoped<JsConsole>();

// Note: No Windows Authentication for external site
// Add your external authentication here later

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// IMPORTANT: Add Content-Security-Policy middleware EARLY in the pipeline
// Must be before UseStaticFiles to protect all responses including static content
// Note: Other security headers (X-Content-Type-Options, X-Frame-Options, etc.) 
// are now configured at the IIS server level to centralize management
app.UseSecurityHeaders();

app.UseStaticFiles();
app.UseRouting();

// Add authentication/authorization when ready
// app.UseAuthentication();
// app.UseAuthorization();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();