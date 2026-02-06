# Security Headers Implementation for CrystalGroupHome.External

## Overview
This implementation adds comprehensive security headers to address pen test findings, including Content Security Policy (CSP) with nonce support.

## Implemented Security Headers

### 1. Content-Security-Policy
**Purpose**: Prevents XSS attacks by controlling which resources can be loaded and executed.

**Configuration**:
- `script-src`: Allows scripts from same origin and with valid nonce. Requires `'unsafe-eval'` for Blazor runtime.
- `style-src`: Allows styles from same origin, with valid nonce, Google Fonts, and cdnjs.
- `font-src`: Allows fonts from same origin, data URIs, Google Fonts, and cdnjs.
- `connect-src`: Allows WebSocket connections for SignalR.
- `frame-ancestors`: Set to `'none'` to prevent clickjacking.
- `upgrade-insecure-requests`: Automatically upgrades HTTP to HTTPS.

**Nonce Implementation**:
- A unique cryptographic nonce is generated per request (32 bytes = 256 bits)
- Stored in `HttpContext.Items["csp-nonce"]`
- Retrieved using `SecurityHelper.GetCspNonce()`
- Must be added to inline scripts and styles: `<script nonce="@nonce">...</script>`

### 2. Strict-Transport-Security (HSTS)
**Purpose**: Forces browsers to use HTTPS for all connections.

**Configuration**:
- `max-age=31536000` (1 year)
- `includeSubDomains` applies to all subdomains
- `preload` eligible for browser preload lists
- Only applied when request is already HTTPS

### 3. X-Content-Type-Options
**Purpose**: Prevents MIME sniffing attacks.

**Configuration**: `nosniff`

### 4. X-Frame-Options
**Purpose**: Defense in depth against clickjacking (supplements CSP frame-ancestors).

**Configuration**: `DENY`

### 5. X-XSS-Protection
**Purpose**: Legacy XSS protection for older browsers.

**Configuration**: `1; mode=block`

### 6. Referrer-Policy
**Purpose**: Controls referrer information leakage.

**Configuration**: `strict-origin-when-cross-origin`

### 7. Permissions-Policy
**Purpose**: Disables unnecessary browser features to reduce attack surface.

**Configuration**: Disables geolocation, microphone, camera, payment, USB, magnetometer, gyroscope, accelerometer.

## Files Created/Modified

### Created Files:
1. `CrystalGroupHome.External/Middleware/SecurityHeadersMiddleware.cs` - Main middleware implementation
2. `CrystalGroupHome.External/Helpers/SecurityHelper.cs` - Helper to retrieve nonce
3. `CrystalGroupHome.External/appsettings.Security.json` - Configuration options

### Modified Files:
1. `CrystalGroupHome.External/Program.cs` - Registered middleware
2. `CrystalGroupHome.External/Pages/_Layout.cshtml` - Added nonce to scripts
3. `CrystalGroupHome.External/Common/MainLayout.razor` - Added nonce support

## How to Use Nonces

### In Razor Pages (.cshtml):
```csharp
@using CrystalGroupHome.External.Helpers

@{
    var nonce = SecurityHelper.GetCspNonce(HttpContext);
}

<script nonce="@nonce">
    // Your inline JavaScript
</script>

<style nonce="@nonce">
    /* Your inline CSS */
</style>
```

### In Blazor Components (.razor):
```razor
@inject IHttpContextAccessor HttpContextAccessor
@using CrystalGroupHome.External.Helpers

@{
    var nonce = SecurityHelper.GetCspNonce(HttpContextAccessor.HttpContext);
}

<style nonce="@nonce">
    .my-component-style { }
</style>
```

### In JavaScript Interop:
External scripts don't need nonces (they're loaded via `<script src="">`), but dynamically created inline scripts would need special handling.

## Testing Your Implementation

### 1. Browser Developer Tools
Open the Console (F12) and check for CSP violations:
```
Refused to execute inline script because it violates the following Content Security Policy directive...
```

### 2. Network Tab
Check Response Headers for your security headers:
- Content-Security-Policy
- Strict-Transport-Security
- X-Content-Type-Options
- X-Frame-Options
- etc.

### 3. Online Security Header Checkers
Test your deployed site with:
- https://securityheaders.com/ (Grade: A+ expected)
- https://observatory.mozilla.org/ (High score expected)

### 4. CSP Report-Only Mode (Testing)
To test without breaking functionality, temporarily change in `SecurityHeadersMiddleware.cs`:
```csharp
context.Response.Headers.Append("Content-Security-Policy-Report-Only",
    // ... your CSP policy
);
```

This will report violations without blocking resources.

### 5. Test Checklist
- [ ] Application loads correctly
- [ ] Blazor interactive components work
- [ ] No CSP violations in console
- [ ] External resources load (Google Fonts, Font Awesome)
- [ ] SignalR connection establishes (check browser Network tab)
- [ ] Forms submit correctly
- [ ] Navigation works
- [ ] Mobile navigation works

## Known Limitations

### Blazor Server Constraints:
1. **Cannot eliminate `'unsafe-eval'`**: Required for Blazor's .NET WebAssembly runtime compilation
2. **WebSocket requirement**: Must allow `wss:` and `ws:` for SignalR
3. **Third-party libraries**: Some may require additional CSP directives

### External Resources:
The CSP is configured to allow:
- Google Fonts (fonts.googleapis.com, fonts.gstatic.com)
- cdnjs.cloudflare.com (Font Awesome)
- Any HTTPS image sources
- Data URIs for images and fonts

## Troubleshooting

### Issue: Blazor not connecting
**Symptom**: "Reconnecting..." message persists
**Solution**: Verify `connect-src 'self' wss: ws:;` is in CSP

### Issue: Styles not loading
**Symptom**: Unstyled content, CSP violation for styles
**Solution**: 
- Check external style sources are in `style-src`
- Ensure inline styles have nonce attribute

### Issue: Scripts blocked
**Symptom**: JavaScript errors, CSP violation for scripts
**Solution**:
- Add nonce to inline scripts
- Verify external scripts are from allowed sources

### Issue: Font Awesome not loading
**Symptom**: Icons appear as squares/boxes
**Solution**: Verify `font-src` and `style-src` include cdnjs.cloudflare.com

## Security Improvements Made

### Before Implementation:
- ? No Content Security Policy
- ? Inline scripts/styles allowed without restriction (XSS risk)
- ? No HSTS header
- ? No X-Frame-Options (clickjacking risk)
- ? Information disclosure via Server headers

### After Implementation:
- ? Strict Content Security Policy with nonce
- ? XSS attack prevention
- ? Clickjacking protection
- ? MIME sniffing prevention
- ? HTTPS enforcement (HSTS)
- ? Server information hidden
- ? Unnecessary browser features disabled

## Compliance

This implementation addresses common pen test findings and compliance requirements:
- ? OWASP Top 10 (A03:2021 - Injection)
- ? OWASP Top 10 (A05:2021 - Security Misconfiguration)
- ? PCI DSS 6.5.7 (Cross-site scripting)
- ? CWE-79 (Cross-site Scripting)
- ? CWE-693 (Protection Mechanism Failure)

## Future Enhancements

### CSP Violation Reporting:
Implement an endpoint to receive CSP violation reports:
```csharp
// Add to CSP header:
report-uri /api/csp-violations

// Create endpoint in Program.cs:
app.MapPost("/api/csp-violations", async (HttpContext context) =>
{
    // Log CSP violations for monitoring
});
```

### Subresource Integrity (SRI):
Add integrity hashes to external resources:
```html
<link rel="stylesheet" 
      href="https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.4.0/css/all.min.css"
      integrity="sha512-..."
      crossorigin="anonymous" />
```

### Environment-Specific CSP:
Adjust CSP strictness based on environment (more permissive in dev, strict in prod).

## Support

For questions or issues with this implementation:
1. Check browser console for CSP violations
2. Review this documentation
3. Test with CSP Report-Only mode first
4. Contact IT Security team if pen test findings persist

---

**Last Updated**: [Current Date]
**Version**: 1.0
**Author**: Crystal Group IT
