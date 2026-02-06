using Microsoft.JSInterop;
using System.Runtime.CompilerServices;

namespace CrystalGroupHome.SharedRCL.Helpers
{
    public class JsConsole
    {
        private readonly IJSRuntime JsRuntime;
        public JsConsole(IJSRuntime jSRuntime)
        {
            JsRuntime = jSRuntime;
        }

        public async Task LogAsync(object toPrint, [CallerMemberName] string callerName = "")
        {
            string logMessage = $"[{callerName}] {toPrint}";
            await JsRuntime.InvokeVoidAsync("console.log", logMessage);
        }
    }
}
