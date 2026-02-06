# Security Headers Testing Guide

## Pre-Deployment Testing

### 1. Local Development Testing

#### Start the Application
```bash
cd CrystalGroupHome.External
dotnet run
```

#### Browser Console Check (F12)
1. Open Developer Tools (F12)
2. Navigate to the Console tab
3. Look for CSP violations (should be none):
   - ? No "Refused to execute inline script" errors
   - ? No "Refused to load stylesheet" errors
   - ? No "Refused to connect" errors

#### Network Tab Check
1. Open Developer Tools (F12)
2. Navigate to the Network tab
3. Reload the page (Ctrl+F5)
4. Click on the document request (usually first in list)
5. Go to "Headers" tab ? "Response Headers"
6. Verify the following headers are present:

```
Content-Security-Policy: default-src 'self'; script-src 'self' 'nonce-...' 'unsafe-eval'; ...
X-Content-Type-Options: nosniff
X-Frame-Options: DENY
X-XSS-Protection: 1; mode=block
Referrer-Policy: strict-origin-when-cross-origin
Permissions-Policy: geolocation=(), microphone=(), ...
Strict-Transport-Security: max-age=31536000; includeSubDomains; preload
```

**Note**: The `Strict-Transport-Security` header will only appear when browsing over HTTPS. It will not be present over HTTP connections.

7. Verify the following headers are NOT present:
```
Server: (should be removed)
X-Powered-By: (should be removed)
X-AspNet-Version: (should be removed)
```

### 2. Functionality Testing

Run through the following checklist:

#### Basic Functionality
- [ ] Application loads without errors
- [ ] Navigation menu works
- [ ] All pages load correctly
- [ ] Forms can be submitted
- [ ] External links work

#### Blazor-Specific Tests
- [ ] Blazor interactive components respond to user input
- [ ] SignalR connection established (check console for connection messages)
- [ ] No "Reconnecting..." issues
- [ ] Component state updates work

#### Visual/Style Tests
- [ ] All styles load correctly
- [ ] Font Awesome icons display properly
- [ ] Google Fonts load correctly
- [ ] Bootstrap styling works
- [ ] Blazorise components styled correctly
- [ ] Mobile responsive design works

#### Vendor Survey Feature Tests
- [ ] Survey forms load and render
- [ ] Survey questions display correctly
- [ ] Survey submissions work
- [ ] Form validations work

### 3. Browser Compatibility Testing

Test in multiple browsers:
- [ ] Chrome/Edge (Chromium)
- [ ] Firefox
- [ ] Safari (if available)

### 4. CSP Report-Only Testing (Optional)

If you want to test without blocking any content first:

1. Temporarily modify `SecurityHeadersMiddleware.cs`:
```csharp
// Change this line:
context.Response.Headers.Append("Content-Security-Policy",

// To this:
context.Response.Headers.Append("Content-Security-Policy-Report-Only",
```

2. Run the application and use it normally
3. Check console for CSP violation reports (won't block, just report)
4. Fix any violations found
5. Change back to "Content-Security-Policy" for enforcement

### 5. SignalR Connection Test

Blazor Server uses SignalR for real-time communication. Verify it works:

1. Open browser console
2. Look for SignalR connection messages:
```
[Information] WebSocket connected to ws://localhost:xxxxx/...
```

3. Test reconnection:
   - Put computer to sleep for 30+ seconds
   - Wake computer
   - Should see "Reconnecting..." briefly, then reconnect automatically

## Post-Deployment Testing

### 1. Online Security Header Checkers

After deploying to your external server, test with these tools:

#### SecurityHeaders.com
1. Go to https://securityheaders.com/
2. Enter your site URL
3. Expected Grade: **A or A+**
4. Review any warnings and adjust if needed

#### Mozilla Observatory
1. Go to https://observatory.mozilla.org/
2. Enter your site URL
3. Expected Score: **90+**
4. Review recommendations

### 2. SSL Labs (HTTPS Configuration)
1. Go to https://www.ssllabs.com/ssltest/
2. Enter your site URL
3. Expected Grade: **A or A+**
4. Verify HSTS is working

### 3. Pen Test Verification

When your security team re-runs pen tests, they should find:

#### Resolved Issues:
- ? Missing Content-Security-Policy header
- ? Missing X-Content-Type-Options header
- ? Missing X-Frame-Options header
- ? Missing HSTS header
- ? Server information disclosure
- ? Clickjacking vulnerability
- ? XSS vulnerability (inline scripts)

## Troubleshooting Common Issues

### Issue: SignalR Won't Connect

**Symptoms:**
- "Reconnecting..." message persists
- Console error: "Refused to connect"

**Solutions:**
1. Check CSP includes: `connect-src 'self' wss: ws:;`
2. Verify WebSocket connection in Network tab (WS filter)
3. Check firewall/proxy allows WebSocket connections

**Test:**
```csharp
// Temporarily add 'unsafe-inline' to test if CSP is the issue
script-src 'self' 'unsafe-inline' 'unsafe-eval';
```

### Issue: Browser Link Connection Error (Development Only)

**Symptoms:**
- Console error: "Connecting to 'http://localhost:...' violates the following Content Security Policy directive"
- Only appears in development environment
- Application still works normally

**Solution:**
This is expected in development and is already handled. The middleware automatically adds `http://localhost:*` and `https://localhost:*` to `connect-src` when running in Development mode. This error may appear if:

1. You're running in a different environment profile
2. The `IHostEnvironment` isn't correctly detecting development mode

**To verify:**
```csharp
// In SecurityHeadersMiddleware.cs, the connect-src should be:
var connectSrc = _environment.IsDevelopment() 
    ? "'self' wss: ws: http://localhost:* https://localhost:*"  // Dev
    : "'self' wss: ws:";  // Production
```

**Note**: Browser Link is a Visual Studio development feature and will not be present in production deployments.

### Issue: Styles Not Loading

**Symptoms:**
- Unstyled content
- Console error: "Refused to apply inline style"

**Solutions:**
1. Verify nonce is added to inline `<style>` tags
2. Check external stylesheets are from allowed sources:
   - Google Fonts: fonts.googleapis.com
   - Font Awesome: cdnjs.cloudflare.com
3. Review `style-src` directive

**Test:**
```html
<!-- View page source, look for nonce in style tags -->
<style nonce="ABC123XYZ...">
```

### Issue: Font Awesome Icons Not Displaying

**Symptoms:**
- Icons appear as squares or are missing
- Console error: "Refused to load font"
- Console error: "MIME type ... is not a supported stylesheet MIME type"

**Solutions:**
1. **Remove incorrect font link tags**: Font files should NOT be loaded with `<link rel="stylesheet">`. They should be loaded via CSS `@font-face` rules or by Font Awesome's CSS.
2. Verify `font-src` includes: `https://cdnjs.cloudflare.com`
3. Verify `style-src` includes: `https://cdnjs.cloudflare.com`
4. Check font files are loading in Network tab

**Common Mistake:**
```html
<!-- WRONG - Don't load fonts as stylesheets -->
<link rel="stylesheet" href="webfonts/fa-solid-900.ttf" />
<link rel="stylesheet" href="webfonts/fa-solid-900.woff2" />

<!-- RIGHT - Let Font Awesome CSS handle fonts -->
<link rel="stylesheet" href="_content/Blazorise.Icons.FontAwesome/v6/css/all.css" />
```

### Issue: Blazor Scripts Not Loading

**Symptoms:**
- Application doesn't become interactive
- Console error: "Refused to execute script"

**Solutions:**
1. Verify `blazor.server.js` has nonce attribute:
   ```html
   <script src="_framework/blazor.server.js" nonce="@nonce"></script>
   ```
2. Check nonce is being generated (not null/empty)
3. Verify `script-src` includes nonce

**Debug:**
```csharp
// Add logging in SecurityHeadersMiddleware.cs
var nonce = GenerateNonce();
Console.WriteLine($"Generated nonce: {nonce}");
context.Items["csp-nonce"] = nonce;
```

### Issue: Third-Party Components Breaking

**Symptoms:**
- Blazorise/other components not working
- Console errors about blocked resources

**Solutions:**
1. Review CSP violations in console
2. Add necessary sources to CSP directives
3. Check if component uses inline styles/scripts
4. Contact component vendor for CSP guidance

## Performance Verification

Security headers should have minimal performance impact:

### Expected Metrics:
- Nonce generation: <1ms per request
- Header addition: <1ms per request
- No impact on Time to First Byte (TTFB)
- No impact on page load time

### Monitoring:
```csharp
// Add to SecurityHeadersMiddleware.cs for monitoring
var sw = Stopwatch.StartNew();
var nonce = GenerateNonce();
sw.Stop();
_logger.LogInformation("Nonce generation took {ElapsedMs}ms", sw.ElapsedMilliseconds);
```

## Regression Testing Checklist

Before deploying to production, test the following flows:

### External Vendor Survey
- [ ] Survey form loads
- [ ] All fields render correctly
- [ ] File uploads work (if applicable)
- [ ] Form validation works
- [ ] Form submission succeeds
- [ ] Success/error messages display

### General Application
- [ ] Login/authentication works (when implemented)
- [ ] All pages accessible via navigation
- [ ] No console errors
- [ ] No broken images/fonts
- [ ] Mobile view works
- [ ] Print functionality works (if applicable)

## Documentation Updates

After successful deployment:
1. Document any CSP adjustments made
2. Update security runbook with new headers
3. Notify security team of changes
4. Update change log
5. Train team on nonce usage for future development

## Support Contacts

If issues persist:
- **Development Team**: [Your team contact]
- **Security Team**: [Security team contact]
- **Infrastructure Team**: [Infra team contact]

## Rollback Procedure

If critical issues are found in production:

1. **Emergency Rollback**:
   ```csharp
   // In Program.cs, comment out:
   // app.UseSecurityHeaders();
   ```

2. **Gradual Rollback** (Preferred):
   ```csharp
   // Change to Report-Only mode in SecurityHeadersMiddleware.cs
   context.Response.Headers.Append("Content-Security-Policy-Report-Only",
   ```

3. **Redeploy** previous version

## Next Steps

After successful implementation:
1. Monitor for CSP violations (if reporting endpoint added)
2. Schedule regular security header audits
3. Update CSP as new external resources are added
4. Train developers on nonce usage
5. Document process for adding new external dependencies

---

**Testing Date**: _____________
**Tester Name**: _____________
**Environment**: ? Dev  ? Staging  ? Production
**Result**: ? Pass  ? Fail (see notes)
**Notes**: 

