namespace CrystalGroupHome.Internal.Common.Data._Epicor
{
    public class EpicorRestSettings
    {
        public string Name { get; set; } = string.Empty;
        public string BlazorDomain { get; set; } = string.Empty;
        public string AppPoolHost { get; set; } = string.Empty;
        public string AppPoolInstance { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string APIKey { get; set; } = string.Empty;
        public string APIVersion { get; set; } = string.Empty;
        public string Company { get; set; } = string.Empty;
        public bool UseApiKey { get; set; } = false;
    }
}