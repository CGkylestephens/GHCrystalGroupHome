# IIS Security Headers - Deployment Configuration

## Overview

Security headers for applications hosted on the same IIS server (including CrystalGroupHome.External, CrystalGroupHome.Internal, and MyCrystal) are now configured at the **IIS Server Level** to:
- Centralize security header management
- Avoid duplicate headers across applications
- Simplify maintenance and updates

## Header Management Strategy

### ? IIS Server Level (Configured in IIS Manager)
The following headers are configured once at the IIS server level and apply to **all applications** on the server:

| Header | Configuration | Purpose |
|--------|---------------|---------|
| `X-Content-Type-Options` | `nosniff` | Prevents MIME sniffing attacks |
| `X-XSS-Protection` | `1; mode=block` | Legacy XSS protection for older browsers |
| `Referrer-Policy` | `strict-origin-when-cross-origin` | Controls referrer information |
| `Permissions-Policy` | See IIS config | Disables unnecessary browser features |
| `Strict-Transport-Security` | `max-age=31536000; includeSubDomains` | Forces HTTPS connections |
| `X-Frame-Options` | `DENY` or `SAMEORIGIN` | Prevents clickjacking |
| `Remove X-Powered-By` | *(removal)* | Prevents information disclosure |
| `Remove Server` | *(removal)* | Prevents information disclosure |

### ? Application Level (Blazor Middleware)
The following header is **application-specific** and configured in each Blazor app's middleware:

| Header | Why Application-Level? |
|--------|------------------------|
| `Content-Security-Policy` | • Requires nonce generation per request<br>• Application-specific resource requirements<br>• Different CSP rules for External vs Internal apps |

## File Changes Made

### CrystalGroupHome.External

#### 1. `Middleware/SecurityHeadersMiddleware.cs`
**Changed:** Removed all security headers except Content-Security-Policy

**Before:**
```csharp
// Set X-Content-Type-Options, X-Frame-Options, X-XSS-Protection, 
// Referrer-Policy, Permissions-Policy, Strict-Transport-Security
// Remove Server, X-Powered-By headers
```

**After:**
```csharp
// Only sets Content-Security-Policy with nonce support
// All other headers managed at IIS level
```

#### 2. `Program.cs`
**Changed:** Removed Kestrel server header configuration

**Before:**
```csharp
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.AddServerHeader = false;
});
```

**After:**
```csharp
// Server header suppression now handled at IIS level
```

#### 3. `web.config`
**Changed:** Removed header configuration sections

**Before:**
```xml
<security>
  <requestFiltering removeServerHeader="true" />
</security>
<httpProtocol>
  <customHeaders>
    <remove name="X-Powered-By" />
    <remove name="X-AspNet-Version" />
  </customHeaders>
</httpProtocol>
```

**After:**
```xml
<!-- Security headers now managed at IIS server level -->
```

#### 4. `appsettings.Security.json`
**Changed:** Simplified to reflect CSP-only management

**Before:**
```json
{
  "SecurityHeaders": {
    "ContentSecurityPolicy": {...},
    "StrictTransportSecurity": {...},
    "FrameOptions": {...},
    // ... many more
  }
}
```

**After:**
```json
{
  "SecurityHeaders": {
    "ContentSecurityPolicy": {
      "Enabled": true,
      "Note": "CSP is application-specific. Other headers at IIS level."
    }
  }
}
```

### CrystalGroupHome.Internal
**No changes needed** - This project never had security header middleware configured. It will inherit security headers from IIS level.

## Testing After Deployment

### 1. Verify No Duplicate Headers
```bash
curl -I https://your-site.com
```

**Check that each security header appears ONLY ONCE:**
- ? If you see duplicate headers ? Misconfiguration
- ? Each header should appear exactly once

### 2. Verify All Required Headers Present
Use browser DevTools (F12) ? Network ? Select any request ? Response Headers

**Expected headers:**
```
Content-Security-Policy: default-src 'self'; ...
X-Content-Type-Options: nosniff
X-Frame-Options: DENY
X-XSS-Protection: 1; mode=block
Referrer-Policy: strict-origin-when-cross-origin
Permissions-Policy: geolocation=(), ...
Strict-Transport-Security: max-age=31536000; includeSubDomains
```

**Headers that should be ABSENT:**
```
? Server: Microsoft-IIS/10.0
? X-Powered-By: ASP.NET
? X-AspNet-Version: ...
```

### 3. Test CSP Functionality
- Verify that nonces are still being generated
- Check browser console for CSP violations (should be none in normal operation)
- Test that Blazor SignalR connections work properly

## IIS Server Configuration Steps

### Required IIS Configuration
These headers must be configured at the IIS **server level** (not site level):

1. **Open IIS Manager**
2. **Select the Server** (not individual sites)
3. **Configure HTTP Response Headers:**
   - Add: `X-Content-Type-Options: nosniff`
   - Add: `X-Frame-Options: DENY`
   - Add: `X-XSS-Protection: 1; mode=block`
   - Add: `Referrer-Policy: strict-origin-when-cross-origin`
   - Add: `Permissions-Policy: geolocation=(), microphone=(), camera=(), payment=(), usb=(), magnetometer=(), gyroscope=(), accelerometer=()`
   - Add: `Strict-Transport-Security: max-age=31536000; includeSubDomains`
   - Remove: `X-Powered-By`
   - Remove: `Server` (requires URL Rewrite module or Registry edit)

### To Remove Server Header (Choose one method):

**Option A: URL Rewrite Module** (Recommended)
```xml
<outboundRules>
  <rule name="Remove Server Header">
    <match serverVariable="RESPONSE_Server" pattern=".+" />
    <action type="Rewrite" value="" />
  </rule>
</outboundRules>
```

**Option B: Registry Edit** (Requires server restart)
```
HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\HTTP\Parameters
DisableServerHeader = 2 (DWORD)
```

## Rollback Plan

If issues occur, you can temporarily re-enable headers in the application:

1. Restore the previous version of `SecurityHeadersMiddleware.cs`
2. Redeploy the application
3. Headers will be set at both IIS and application level (safe but redundant)
4. Investigate and fix IIS configuration
5. Remove application-level headers again once IIS is configured correctly

## Benefits of This Approach

? **Centralized Management:** Update security headers once for all applications
? **No Duplication:** Each header set only once
? **Consistency:** All applications on server have same security baseline
? **Maintainability:** Easier to update security policies
? **Performance:** Slightly reduced middleware overhead in applications

## Migration Checklist

- [ ] IIS server-level security headers configured
- [ ] CrystalGroupHome.External code updated (this branch)
- [ ] CrystalGroupHome.External deployed and tested
- [ ] CrystalGroupHome.Internal deployed and tested (no code changes needed)
- [ ] MyCrystal updated and deployed
- [ ] All applications tested for duplicate headers
- [ ] Security scan performed (securityheaders.com)
- [ ] Documentation updated
- [ ] Team notified of new configuration

## Related Documentation

- `SECURITY_HEADERS_README.md` - Original security headers documentation
- `IIS_DEPLOYMENT_GUIDE.md` - General IIS deployment guidance
- `SECURITY_TESTING_GUIDE.md` - How to test security headers

## Questions?

Contact IT Security or the development team for assistance with IIS configuration or security header issues.

---

**Last Updated:** [Date]
**Branch:** feature/content-security-policy-updates
**Related Ticket:** [Pen Test Remediation]
