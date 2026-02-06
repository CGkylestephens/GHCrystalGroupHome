using System.Text.Json;
using System.Runtime.CompilerServices;
using CrystalGroupHome.SharedRCL.Services;

namespace CrystalGroupHome.SharedRCL.Helpers
{
    public class DebugModeService
    {
        private bool _isDebugMode;
        public bool IsDebugMode => _isDebugMode;

        /// <summary>
        /// The impersonation service for managing persona-based impersonation
        /// </summary>
        public ImpersonationService Impersonation { get; }

        public event Action? OnChange;

        private readonly JsConsole _jsConsole;

        public void Toggle()
        {
            _isDebugMode = !_isDebugMode;
            if (!_isDebugMode)
            {
                // Stop any active impersonation when debug mode is turned off
                Impersonation.StopImpersonation();
            }
            OnChange?.Invoke();
        }

        /// <summary>
        /// Starts impersonation with a predefined persona
        /// </summary>
        /// <param name="personaId">The ID of the persona to impersonate</param>
        /// <param name="isProduction">Whether this is a production environment</param>
        /// <returns>True if impersonation was started successfully</returns>
        public bool StartImpersonation(string personaId, bool isProduction)
        {
            if (!_isDebugMode || isProduction)
                return false;

            var result = Impersonation.StartImpersonation(personaId);
            if (result)
            {
                OnChange?.Invoke();
            }
            return result;
        }

        /// <summary>
        /// Stops any active impersonation
        /// </summary>
        public void StopImpersonation()
        {
            Impersonation.StopImpersonation();
            OnChange?.Invoke();
        }

        /// <summary>
        /// Gets a summary of the current impersonation state for display
        /// </summary>
        public string GetImpersonationSummary()
        {
            if (!Impersonation.IsImpersonating || Impersonation.ActivePersona == null)
                return string.Empty;

            var persona = Impersonation.ActivePersona;
            return $"Impersonating: {persona.Name}";
        }

        public DebugModeService(JsConsole jsConsole, ImpersonationService impersonationService)
        {
            _jsConsole = jsConsole;
            Impersonation = impersonationService;
            
            // Subscribe to impersonation changes to propagate notifications
            Impersonation.OnImpersonationChanged += () => OnChange?.Invoke();
        }

        private static readonly JsonSerializerOptions s_writeOptions = new()
        {
            WriteIndented = true
        };

        public async Task SqlQueryDebugMessage<T>(string sql, T result, [CallerMemberName] string callerName = "")
        {
            if (IsDebugMode)
            {
                var resultJson = JsonSerializer.Serialize(result, s_writeOptions);
                var consoleLog = $@"     
[{callerName}]
                    
SQL: {sql}

Result: {resultJson}
";
                await _jsConsole.LogAsync(consoleLog);
            }
        }

        public async Task PrintToConsole(object objectToPrint)
        {
            if (IsDebugMode)
            {
                await _jsConsole.LogAsync(objectToPrint);
            }
        }
    }
}
