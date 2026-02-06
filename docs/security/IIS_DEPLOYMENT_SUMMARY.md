# IIS Deployment - Quick Summary

## What Changed for IIS Deployment

### ? New File Created: `web.config`

**Purpose**: Configure IIS-specific security settings that can't be controlled from C# code.

**Key Settings:**
```xml
<!-- Remove Server header (IIS adds this) -->
<security>
  <requestFiltering removeServerHeader="true" />
</security>

<!-- Remove X-Powered-By header (IIS adds this) -->
<httpProtocol>
  <customHeaders>
    <remove name="X-Powered-By" />
  </customHeaders>
</httpProtocol>

<!-- In-process hosting (better performance + ensures middleware runs) -->
<aspNetCore hostingModel="inprocess" ... />

<!-- HTTPS redirect rule -->
<rewrite>
  <rules>
    <rule name="HTTPS Redirect" ... />
  </rules>
</rewrite>
```

## Why This Matters

| Without web.config | With web.config |
|-------------------|-----------------|
| ? Server: Microsoft-IIS/10.0 | ? Server header removed |
| ? X-Powered-By: ASP.NET | ? X-Powered-By removed |
| ?? May bypass middleware | ? All requests through middleware |
| ?? HTTP accessible | ? Auto-redirect to HTTPS |

## Deployment Process

### Step 1: Publish
```bash
dotnet publish -c Release -o [deployment-folder]
```

### Step 2: Verify web.config
- ? Should be automatically included in publish output
- ? Located in deployment folder root

### Step 3: IIS Configuration
```
Application Pool:
  - .NET CLR Version: No Managed Code
  - Pipeline Mode: Integrated
  
Website:
  - Binding: HTTPS (with certificate)
  - Physical Path: [deployment-folder]
```

### Step 4: Test Security Headers
**Browser DevTools (F12) ? Network ? Check Response Headers:**
```
? Content-Security-Policy: ...
? X-Content-Type-Options: nosniff
? X-Frame-Options: DENY
? Strict-Transport-Security: ...
? Server: (should be absent)
? X-Powered-By: (should be absent)
```

## Important Notes

### In-Process Hosting
```xml
<aspNetCore hostingModel="inprocess" ... />
```
- ? Better performance
- ? Ensures middleware runs for all requests
- ? Security headers apply to static files
- **Recommended for all environments**

### Server Header Removal
- **Local (Kestrel)**: `AddServerHeader = false` in Program.cs ?
- **IIS**: `removeServerHeader="true"` in web.config ?
- **Both are needed** for complete coverage

### HTTPS in IIS
- IIS typically handles SSL/TLS termination
- Your middleware correctly detects HTTPS via `context.Request.IsHttps`
- HSTS header will automatically be added for HTTPS requests

## Files Summary

| File | Purpose | Status |
|------|---------|--------|
| `web.config` | IIS configuration | ? Created |
| `SecurityHeadersMiddleware.cs` | Add security headers | ? Already configured |
| `Program.cs` | Kestrel + middleware setup | ? Already configured |
| `IIS_DEPLOYMENT_GUIDE.md` | Detailed deployment instructions | ? Created |

## Testing in Each Environment

### Development (IIS)
```bash
# After deployment, test:
curl -I https://dev.yoursite.com
```
**Look for:**
- ? Security headers present
- ? Server header absent

### Pilot (IIS)
- Same testing as Development
- Verify with real SSL certificate
- Test all application features

### Production (IIS)
- Same testing as Pilot
- Run online security scans:
  - https://securityheaders.com/
  - https://observatory.mozilla.org/

## Quick Troubleshooting

### Problem: Server header still appears
**Solution**: 
1. Check IIS version (needs 10.0+ for `removeServerHeader`)
2. For older IIS, use URL Rewrite module (see IIS_DEPLOYMENT_GUIDE.md)

### Problem: Security headers missing
**Solution**:
1. Verify `hostingModel="inprocess"` in web.config
2. Check middleware registration order in Program.cs
3. Restart Application Pool

### Problem: Application won't start
**Solution**:
1. Check Event Viewer ? Application logs
2. Enable stdout logging in web.config
3. Verify .NET 9 Runtime installed

## Ready to Deploy ?

Your application is now configured for secure IIS deployment with:
- ? Comprehensive security headers
- ? CSP with nonce support
- ? Server information disclosure prevention
- ? HTTPS enforcement
- ? IIS-specific optimizations

**Next Step**: Deploy to Development IIS and test!

---

**See**: `IIS_DEPLOYMENT_GUIDE.md` for detailed deployment instructions.
