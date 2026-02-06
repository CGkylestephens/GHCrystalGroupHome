# IIS Deployment Guide - Security Headers

## Overview
When deploying to IIS, additional configuration is needed to ensure security headers work correctly.

## Key Differences: Kestrel vs IIS

| Aspect | Kestrel (Local) | IIS (Dev/Pilot/Prod) |
|--------|----------------|---------------------|
| Server Header | Controlled by `AddServerHeader = false` | Requires `web.config` |
| X-Powered-By | Removed by middleware | Requires `web.config` |
| Static Files | Goes through middleware | May bypass middleware |
| HTTPS Termination | Kestrel | IIS (usually) |
| Hosting Model | Out-of-process or In-process | In-process recommended |

## Required Files for IIS Deployment

### 1. web.config (? Created)
The `web.config` file has been created with:
- ? Server header removal (`removeServerHeader="true"`)
- ? X-Powered-By header removal
- ? ASP.NET version header removal
- ? HTTPS redirect rule
- ? In-process hosting model

**Location**: `CrystalGroupHome.External/web.config`

### 2. SecurityHeadersMiddleware.cs (? Already Configured)
Your middleware is properly configured and will work in IIS with `hostingModel="inprocess"`.

### 3. Program.cs (? Already Configured)
Kestrel configuration is still used in in-process mode.

## IIS Configuration Steps

### Development Environment

1. **Publish the Application**
   ```bash
   dotnet publish -c Release -o C:\inetpub\wwwroot\CrystalGroupHome.External
   ```

2. **Create IIS Application Pool**
   - Name: `CrystalGroupHome.External`
   - .NET CLR Version: **No Managed Code**
   - Managed Pipeline Mode: Integrated
   - Identity: ApplicationPoolIdentity (or appropriate service account)

3. **Create IIS Website/Application**
   - Site Name: CrystalGroupHome.External
   - Physical Path: `C:\inetpub\wwwroot\CrystalGroupHome.External`
   - Application Pool: CrystalGroupHome.External
   - Binding: https://*:443 (with SSL certificate)

4. **Verify web.config is Deployed**
   - Check that `web.config` exists in the deployment folder
   - Verify the `<aspNetCore>` element has `hostingModel="inprocess"`

5. **Restart Application Pool**
   ```powershell
   Restart-WebAppPool -Name "CrystalGroupHome.External"
   ```

### Pilot/Production Environments

Follow the same steps as Development, with these additions:

1. **Environment Variable**
   - Set `ASPNETCORE_ENVIRONMENT` to `Production` or `Staging`
   - Can be set in web.config (already configured) or IIS Application Settings

2. **SSL Certificate**
   - Ensure valid SSL certificate is installed and bound to the site
   - HSTS header requires HTTPS

3. **Permissions**
   - Application Pool identity needs read access to deployment folder
   - Application Pool identity needs read/write access to any file storage paths

## Testing in IIS

### 1. Verify Security Headers

After deployment, test with browser DevTools:

**Open Developer Tools (F12) ? Network Tab**
1. Navigate to your site
2. Click on the document request
3. Check **Response Headers**

**Expected Headers:**
```
Content-Security-Policy: default-src 'self'; script-src 'self' 'nonce-...' 'unsafe-eval'; ...
X-Content-Type-Options: nosniff
X-Frame-Options: DENY
X-XSS-Protection: 1; mode=block
Referrer-Policy: strict-origin-when-cross-origin
Permissions-Policy: geolocation=(), microphone=(), ...
Strict-Transport-Security: max-age=31536000; includeSubDomains; preload
```

**Headers That Should NOT Be Present:**
```
Server: (should be removed)
X-Powered-By: (should be removed)
X-AspNet-Version: (should be removed)
X-AspNetMvc-Version: (should be removed)
```

### 2. Test Static Files

Test that security headers apply to static files:

**Check a CSS file:**
```
https://yoursite.com/_content/Blazorise/blazorise.css
```

**Expected Headers (in Response):**
- ? Content-Security-Policy
- ? X-Content-Type-Options: nosniff
- ? X-Frame-Options: DENY
- ? Server (should be absent)

If security headers are missing on static files:
- Verify `hostingModel="inprocess"` in web.config
- Ensure middleware is registered before `UseStaticFiles()` in Program.cs

### 3. Test HTTPS Redirect

Visit HTTP version:
```
http://yoursite.com
```

**Expected**: Automatic redirect to HTTPS
```
https://yoursite.com
```

### 4. Test Application Functionality

- ? Application loads
- ? Blazor components work
- ? SignalR connection established
- ? Forms submit correctly
- ? Vendor survey works

## Common IIS Issues & Solutions

### Issue: Server Header Still Present

**Symptoms:**
```
Server: Microsoft-IIS/10.0
```

**Solutions:**

1. **Verify web.config has the setting:**
   ```xml
   <security>
     <requestFiltering removeServerHeader="true" />
   </security>
   ```

2. **Check IIS version** - `removeServerHeader` requires:
   - IIS 10.0+ (Windows Server 2016+)
   - For older IIS, use URL Rewrite module to remove header

3. **Alternative for older IIS versions:**
   ```xml
   <rewrite>
     <outboundRules>
       <rule name="Remove Server Header">
         <match serverVariable="RESPONSE_Server" pattern=".+" />
         <action type="Rewrite" value="" />
       </rule>
     </outboundRules>
   </rewrite>
   ```

### Issue: Security Headers Missing

**Symptoms:**
- Security headers present in local development
- Missing when deployed to IIS

**Solutions:**

1. **Check hosting model:**
   ```xml
   <aspNetCore hostingModel="inprocess" ... />
   ```
   - Use `inprocess` (recommended)
   - `outofprocess` may cause issues

2. **Verify middleware registration:**
   ```csharp
   // In Program.cs - should be early in pipeline
   app.UseSecurityHeaders();
   app.UseStaticFiles();
   ```

3. **Check Application Pool:**
   - .NET CLR Version: **No Managed Code**
   - Managed Pipeline Mode: Integrated

### Issue: HSTS Not Working

**Symptoms:**
- Strict-Transport-Security header missing

**Solutions:**

1. **Verify HTTPS binding** in IIS
   - Site must be accessed via HTTPS
   - `context.Request.IsHttps` must be true

2. **Check SSL certificate** is valid
   - Self-signed certs may not trigger HSTS in browsers

3. **Force HTTPS redirect** in web.config (already configured)

### Issue: Application Won't Start

**Symptoms:**
- 502.5 error
- Application pool crashes

**Solutions:**

1. **Check Event Viewer:**
   - Windows Logs ? Application
   - Look for ASP.NET Core errors

2. **Enable stdout logging:**
   ```xml
   <aspNetCore stdoutLogEnabled="true" 
               stdoutLogFile=".\logs\stdout" ... />
   ```

3. **Check permissions:**
   - Application Pool identity has read access
   - Can create logs folder

4. **Verify .NET Runtime installed:**
   - ASP.NET Core Runtime 9.0+ required
   - Hosting Bundle for IIS

## IIS-Specific Security Enhancements

### Optional: Additional IIS Hardening

Add to web.config for extra security:

```xml
<security>
  <requestFiltering>
    <!-- Remove Server header -->
    <removeServerHeader>true</removeServerHeader>
    
    <!-- Limit request size (DOS protection) -->
    <requestLimits maxAllowedContentLength="104857600" />
    
    <!-- Block potentially dangerous HTTP verbs -->
    <verbs>
      <remove verb="TRACE" />
      <remove verb="TRACK" />
    </verbs>
    
    <!-- Hide file extensions -->
    <hiddenSegments>
      <add segment="web.config" />
      <add segment="appsettings.json" />
    </hiddenSegments>
  </requestFiltering>
</security>
```

### Optional: Compression Configuration

```xml
<urlCompression doStaticCompression="true" 
                doDynamicCompression="true" />
```

## Deployment Checklist

### Pre-Deployment
- [ ] Build project in Release mode
- [ ] web.config is included in publish output
- [ ] Test locally with IIS Express
- [ ] Review appsettings.Production.json

### Deployment
- [ ] Stop Application Pool
- [ ] Copy files to IIS directory
- [ ] Verify web.config exists
- [ ] Start Application Pool
- [ ] Check Event Viewer for errors

### Post-Deployment Testing
- [ ] Application loads without errors
- [ ] Check Response Headers (F12 ? Network)
- [ ] Verify Server header is removed
- [ ] Test HTTPS redirect
- [ ] Test Blazor functionality
- [ ] Test vendor survey feature
- [ ] Run online security scan (securityheaders.com)

### Security Verification
- [ ] No Server header
- [ ] No X-Powered-By header
- [ ] CSP header present with nonce
- [ ] HSTS header present (HTTPS only)
- [ ] All security headers present
- [ ] Test with security scanning tools

## Troubleshooting Tools

### PowerShell Commands

**Restart Application Pool:**
```powershell
Restart-WebAppPool -Name "CrystalGroupHome.External"
```

**Check Application Pool Status:**
```powershell
Get-WebAppPoolState -Name "CrystalGroupHome.External"
```

**Test HTTP Response:**
```powershell
$response = Invoke-WebRequest -Uri "https://yoursite.com" -Method Get
$response.Headers
```

### IIS Manager
- Event Viewer ? Windows Logs ? Application
- IIS Manager ? Application Pools ? Advanced Settings
- IIS Manager ? Sites ? Your Site ? Failed Request Tracing

## Additional Resources

- [ASP.NET Core Module (ANCM) for IIS](https://docs.microsoft.com/en-us/aspnet/core/host-and-deploy/aspnet-core-module)
- [IIS Host ASP.NET Core](https://docs.microsoft.com/en-us/aspnet/core/host-and-deploy/iis/)
- [IIS Security Hardening](https://docs.microsoft.com/en-us/iis/manage/configuring-security/)
- [Troubleshoot ASP.NET Core on IIS](https://docs.microsoft.com/en-us/aspnet/core/test/troubleshoot-azure-iis)

## Support

If issues persist after deployment:
1. Check Event Viewer logs
2. Enable stdout logging in web.config
3. Verify IIS configuration matches this guide
4. Test with online security scanning tools

---

**Last Updated**: December 16, 2025
**Target Environments**: Development, Pilot, Production (IIS)
**Status**: ? Ready for IIS Deployment
