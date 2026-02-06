# Quick Testing Reference Card

## After Deploying These Changes

### ? Quick Header Check (PowerShell)
```powershell
# Test External app
$response = Invoke-WebRequest -Uri "https://your-external-site.com" -Method Head
$response.Headers

# Test Internal app  
$response = Invoke-WebRequest -Uri "https://your-internal-site.com" -Method Head -UseDefaultCredentials
$response.Headers
```

### ? Quick Header Check (curl)
```bash
curl -I https://your-site.com
```

### ? Browser DevTools Check
1. Open site in browser
2. Press F12
3. Go to Network tab
4. Refresh page
5. Click on any request
6. View Response Headers

---

## ? What You Should See (Each header exactly ONCE)

```
HTTP/1.1 200 OK
Content-Security-Policy: default-src 'self'; script-src 'self' 'unsafe-inline' 'unsafe-eval'; ...
X-Content-Type-Options: nosniff
X-Frame-Options: DENY
X-XSS-Protection: 1; mode=block
Referrer-Policy: strict-origin-when-cross-origin
Permissions-Policy: geolocation=(), microphone=(), camera=(), ...
Strict-Transport-Security: max-age=31536000; includeSubDomains
Content-Type: text/html; charset=utf-8
Date: [date]
```

---

## ? What You Should NOT See

```
Server: Microsoft-IIS/10.0          ? Should be absent
X-Powered-By: ASP.NET               ? Should be absent
X-AspNet-Version: 4.0.30319         ? Should be absent

# OR duplicate headers:
X-Frame-Options: DENY               ? Should appear only once
X-Frame-Options: DENY               ? Duplicate = misconfiguration
```

---

## ?? If Headers Are Missing

**Problem:** Security headers not present at all

**Cause:** IIS server-level configuration not complete

**Solution:**
1. Open IIS Manager
2. Select **Server** (not individual sites)
3. Double-click "HTTP Response Headers"
4. Add missing headers (see IIS_SECURITY_HEADERS_DEPLOYMENT.md)

---

## ?? If Headers Are Duplicated

**Problem:** Same header appears multiple times

**Cause:** Headers configured at both IIS and application level

**Solution:**
1. Check if this code has been deployed (should remove app-level headers)
2. Check if IIS server-level headers are configured
3. Restart IIS Application Pool
4. Clear browser cache and retest

---

## ?? Security Scan Sites

Quick security validation:
- https://securityheaders.com/
- https://observatory.mozilla.org/

**Target Grade:** A or A+

---

## ?? Checklist for Deployment Day

**Before Deployment:**
- [ ] IIS server-level headers configured
- [ ] Tested on development IIS environment
- [ ] No duplicate headers confirmed
- [ ] Team notified of deployment

**During Deployment:**
- [ ] Application published
- [ ] Files copied to IIS folder
- [ ] Application pool restarted
- [ ] Initial smoke test passed

**After Deployment:**
- [ ] Headers checked (use curl or browser)
- [ ] No duplicates confirmed
- [ ] Application functions normally
- [ ] Blazor SignalR connection works
- [ ] Security scan performed
- [ ] Results documented

---

## ?? Emergency Rollback

If critical issues occur:

```bash
# Navigate to repo
cd C:\path\to\CrystalGroupHome

# Revert changes
git revert HEAD

# Rebuild and redeploy
dotnet publish -c Release -o C:\deployment\folder

# Restart IIS App Pool
Restart-WebAppPool -Name "YourAppPoolName"
```

This will restore headers at application level (causing duplicates but ensuring security).

---

## ?? Who to Contact

| Issue | Contact |
|-------|---------|
| IIS configuration | IT Operations / [Name] |
| Application errors | Development Team / [Name] |
| Security concerns | Security Team / [Name] |
| Deployment issues | DevOps / [Name] |

---

## ?? Full Documentation

See these files for complete details:
- `IIS_SECURITY_HEADERS_DEPLOYMENT.md` - Complete deployment guide
- `CHANGES_SUMMARY.md` - All changes made
- `SECURITY_HEADERS_README.md` - Original security documentation

---

**Quick Tip:** Bookmark this file for easy reference during deployment!
