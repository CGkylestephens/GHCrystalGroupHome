# Security Documentation

This folder contains security-related documentation for the CrystalGroupHome applications.

## Overview

These documents were created as part of the penetration testing remediation work for implementing proper security headers across all CrystalGroupHome applications (External, Internal, and related apps) hosted on shared IIS infrastructure.

## Documents

### Deployment & Configuration

- **[IIS_SECURITY_HEADERS_DEPLOYMENT.md](IIS_SECURITY_HEADERS_DEPLOYMENT.md)**  
  Main deployment guide explaining how security headers are split between IIS server level and application level. **Start here** for understanding the overall approach.

- **[IIS_DEPLOYMENT_GUIDE.md](IIS_DEPLOYMENT_GUIDE.md)**  
  Detailed IIS deployment instructions and configuration steps.

- **[IIS_DEPLOYMENT_SUMMARY.md](IIS_DEPLOYMENT_SUMMARY.md)**  
  Quick summary of IIS deployment configuration.

### Security Implementation

- **[SECURITY_HEADERS_README.md](SECURITY_HEADERS_README.md)**  
  Comprehensive guide to security headers implementation, what each header does, and why it's important.

- **[SECURITY_FIXES_APPLIED.md](SECURITY_FIXES_APPLIED.md)**  
  Complete audit trail of security fixes applied to address penetration testing findings.

### Testing & Validation

- **[SECURITY_TESTING_GUIDE.md](SECURITY_TESTING_GUIDE.md)**  
  How to test and validate security headers in different environments.

- **[QUICK_TEST_REFERENCE.md](QUICK_TEST_REFERENCE.md)**  
  Quick reference card for testing security headers after deployment.

### Change Documentation

- **[CHANGES_SUMMARY.md](CHANGES_SUMMARY.md)**  
  Summary of all changes made to centralize security header management at IIS level.

## Key Concepts

### Two-Tier Security Header Strategy

**IIS Server Level (Centralized):**
- X-Content-Type-Options
- X-Frame-Options
- X-XSS-Protection
- Referrer-Policy
- Permissions-Policy
- Strict-Transport-Security
- Server/X-Powered-By header removal

**Application Level (App-Specific):**
- Content-Security-Policy (CSP)
  - Requires per-request nonce generation
  - Application-specific resource requirements
  - Cannot be centralized at IIS level

### Why This Approach?

1. **Avoid Duplication**: Multiple apps on same IIS server don't duplicate headers
2. **Centralized Management**: Update security policies once for all apps
3. **Consistency**: All applications have same security baseline
4. **Flexibility**: Application-specific headers (like CSP) remain in app code

## Quick Start

### For Developers
1. Read `SECURITY_HEADERS_README.md` to understand what each header does
2. Review `CHANGES_SUMMARY.md` to see what changed in the codebase
3. Use `QUICK_TEST_REFERENCE.md` when testing deployments

### For IT/Operations
1. Start with `IIS_SECURITY_HEADERS_DEPLOYMENT.md` for the big picture
2. Follow `IIS_DEPLOYMENT_GUIDE.md` for step-by-step configuration
3. Use `SECURITY_TESTING_GUIDE.md` to validate configuration

### For Security Team
1. Review `SECURITY_FIXES_APPLIED.md` for audit trail
2. Use `SECURITY_TESTING_GUIDE.md` for validation procedures
3. Reference `SECURITY_HEADERS_README.md` for header specifications

## Related Files in Codebase

### CrystalGroupHome.External
- `Middleware/SecurityHeadersMiddleware.cs` - CSP implementation
- `Helpers/SecurityHelper.cs` - Nonce retrieval helper
- `Pages/_Layout.cshtml` - CSP nonce injection in layout
- `web.config` - IIS/ASP.NET Core configuration
- `appsettings.Security.json` - Security configuration
- `Program.cs` - Middleware registration

### CrystalGroupHome.Internal
- Uses the same CSP approach but with Windows Authentication
- No separate security middleware needed (can be added if needed)
- Inherits IIS-level security headers automatically

## Branch Information

- **Feature Branch**: `feature/content-security-policy-updates`
- **Related Work**: Penetration testing remediation
- **Azure DevOps**: [Link to work item if applicable]

## Contact

For questions about security implementation:
- Development Team: [contact]
- IT Operations: [contact]
- Security Team: [contact]

---

**Last Updated**: January 2026  
**Status**: Active - In deployment phase
