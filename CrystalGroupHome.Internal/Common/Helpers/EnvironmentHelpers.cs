namespace CrystalGroupHome.Internal.Common.Helpers
{
    public class EnvironmentHelpers
    {
        private readonly IWebHostEnvironment _env;

        public EnvironmentHelpers(IWebHostEnvironment env)
        {
            _env = env;
        }

        public void PrintEnvironment()
        {
            Console.WriteLine($"Current Environment: {_env.EnvironmentName}");
        }

        public string GetEnvironment()
        {
            return _env.EnvironmentName;
        }

        public bool IsProduction()
        {
            return _env.IsProduction();
        }

        public bool IsDevelopment()
        {
            return _env.IsDevelopment();
        }

        public bool IsStaging()
        {
            return _env.IsStaging();
        }
    }
}
