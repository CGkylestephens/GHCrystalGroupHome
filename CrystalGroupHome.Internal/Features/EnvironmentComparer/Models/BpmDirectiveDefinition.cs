using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CrystalGroupHome.Internal.Features.EnvironmentComparer.Models
{
    public class BpmDirectiveDefinition
    {
        // BpDirective properties
        public string Source { get; set; } = string.Empty;
        public int DirectiveType { get; set; }
        public bool IsEnabled { get; set; }
        public string BpMethodCode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string DirectiveID { get; set; } = string.Empty;
        public int Order { get; set; }
        public string DirectiveGroup { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Body { get; set; } = string.Empty;

        // BpMethod properties
        public string MethodSource { get; set; } = string.Empty;
        public string SystemCode { get; set; } = string.Empty;
        public string ObjectNS { get; set; } = string.Empty;
        public string BusinessObject { get; set; } = string.Empty;
        public string MethodName { get; set; } = string.Empty;
        public string? MethodDescription { get; set; }
        public string? Version { get; set; }
        public bool HasRootTransaction { get; set; }
        public bool DebugMode { get; set; }
        public bool DumpSources { get; set; }
        public bool AdvTracing { get; set; }

        // Computed properties
        public string ContentHash { get; private set; } = string.Empty;

        public void ComputeContentHash()
        {
            using var sha256 = SHA256.Create();
            var content = new
            {
                BpMethodCode,
                IsEnabled,
                Body,
                Order,
                DirectiveType
            };
            var json = JsonSerializer.Serialize(content);
            var bytes = Encoding.UTF8.GetBytes(json);
            var hash = sha256.ComputeHash(bytes);
            ContentHash = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        public string GetUniqueIdentifier() => DirectiveID;
    }
}