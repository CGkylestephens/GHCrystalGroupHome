using CrystalGroupHome.SharedRCL.Models;

namespace CrystalGroupHome.SharedRCL.Services;

/// <summary>
/// Service for managing user impersonation during testing and debugging.
/// Allows developers and admins to simulate different user permission sets
/// to verify UI behavior without needing to log in as different users.
/// 
/// IMPORTANT: Impersonation is completely disabled in production environments.
/// </summary>
public class ImpersonationService
{
    private ImpersonationPersona? _activePersona;
    private bool _isImpersonating;
    private bool _isProductionEnvironment;

    /// <summary>
    /// Event fired when impersonation state changes (activated, deactivated, or persona changed)
    /// </summary>
    public event Action? OnImpersonationChanged;

    /// <summary>
    /// Whether impersonation is currently active.
    /// Always returns false in production environments.
    /// </summary>
    public bool IsImpersonating => !_isProductionEnvironment && _isImpersonating;

    /// <summary>
    /// The currently active impersonation persona, if any.
    /// Always returns null in production environments.
    /// </summary>
    public ImpersonationPersona? ActivePersona => _isProductionEnvironment ? null : _activePersona;

    /// <summary>
    /// Gets all available personas that can be impersonated.
    /// Returns empty list in production environments.
    /// </summary>
    public IReadOnlyList<ImpersonationPersona> AvailablePersonas => 
        _isProductionEnvironment ? [] : ImpersonationPersona.GetAvailablePersonas();

    /// <summary>
    /// Whether impersonation features are available (false in production)
    /// </summary>
    public bool IsImpersonationAvailable => !_isProductionEnvironment;

    /// <summary>
    /// Configures whether this is a production environment.
    /// Should be called during initialization from MainLayout.
    /// </summary>
    public void SetProductionMode(bool isProduction)
    {
        _isProductionEnvironment = isProduction;
        
        // If switching to production mode while impersonating, stop immediately
        if (isProduction && _isImpersonating)
        {
            _activePersona = null;
            _isImpersonating = false;
            OnImpersonationChanged?.Invoke();
        }
    }

    /// <summary>
    /// Starts impersonation with the specified persona.
    /// Does nothing in production environments.
    /// </summary>
    /// <param name="persona">The persona to impersonate</param>
    public void StartImpersonation(ImpersonationPersona persona)
    {
        // Never allow impersonation in production
        if (_isProductionEnvironment)
            return;

        _activePersona = persona;
        _isImpersonating = true;
        OnImpersonationChanged?.Invoke();
    }

    /// <summary>
    /// Starts impersonation by persona ID.
    /// Does nothing in production environments.
    /// </summary>
    /// <param name="personaId">The unique ID of the persona to impersonate</param>
    /// <returns>True if persona was found and impersonation started, false otherwise</returns>
    public bool StartImpersonation(string personaId)
    {
        // Never allow impersonation in production
        if (_isProductionEnvironment)
            return false;

        var persona = ImpersonationPersona.GetAvailablePersonas().FirstOrDefault(p => p.Id == personaId);
        if (persona == null)
            return false;

        StartImpersonation(persona);
        return true;
    }

    /// <summary>
    /// Stops impersonation and returns to the real user's permissions
    /// </summary>
    public void StopImpersonation()
    {
        _activePersona = null;
        _isImpersonating = false;
        OnImpersonationChanged?.Invoke();
    }

    /// <summary>
    /// Checks if the impersonated persona has the specified role.
    /// Always returns false in production environments.
    /// </summary>
    /// <param name="role">The role to check</param>
    /// <returns>True if impersonating and the persona has the role, false otherwise</returns>
    public bool HasRole(string role)
    {
        // Never allow impersonation in production
        if (_isProductionEnvironment)
            return false;

        if (!_isImpersonating || _activePersona == null)
            return false;

        return _activePersona.Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if the impersonated persona explicitly does NOT have a role
    /// (used when impersonation is active to determine if we should deny access).
    /// Always returns false in production environments.
    /// </summary>
    /// <param name="role">The role to check</param>
    /// <returns>True if impersonating and the persona does NOT have the role</returns>
    public bool IsDeniedRole(string role)
    {
        // Never allow impersonation in production
        if (_isProductionEnvironment)
            return false;

        if (!_isImpersonating || _activePersona == null)
            return false;

        return !_activePersona.Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Creates a custom persona with the specified roles for ad-hoc testing.
    /// Does nothing in production environments.
    /// </summary>
    /// <param name="name">Display name for the custom persona</param>
    /// <param name="roles">The roles to assign to this persona</param>
    public void StartCustomImpersonation(string name, IEnumerable<string> roles)
    {
        // Never allow impersonation in production
        if (_isProductionEnvironment)
            return;

        var customPersona = new ImpersonationPersona(
            id: "custom",
            name: name,
            description: "Custom persona created for testing",
            roles: roles.ToList()
        );
        StartImpersonation(customPersona);
    }
}
