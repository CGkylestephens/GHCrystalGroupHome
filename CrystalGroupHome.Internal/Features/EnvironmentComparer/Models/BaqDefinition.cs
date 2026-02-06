using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CrystalGroupHome.Internal.Features.EnvironmentComparer.Models
{
    public class BaqDefinition
    {
        public string Company { get; set; } = string.Empty;
        public string QueryID { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsShared { get; set; }
        public string AuthorID { get; set; } = string.Empty;
        public string DisplayPhrase { get; set; } = string.Empty;
        public string ContentHash { get; private set; } = string.Empty;

        public void ComputeContentHash()
        {
            using var sha256 = SHA256.Create();
            var content = new
            {
                Company,
                QueryID,
                Description,
                IsShared,
                AuthorID,
                DisplayPhrase
            };
            var json = JsonSerializer.Serialize(content);
            var bytes = Encoding.UTF8.GetBytes(json);
            var hash = sha256.ComputeHash(bytes);
            ContentHash = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
    }
}