using Microsoft.Extensions.DependencyInjection;
using Sidekick.Business;
using Sidekick.Core;
using Sidekick.Localization;
using Sidekick.UI;

namespace Sidekick
{
    public static class Startup
    {
        public static ServiceProvider InitializeServices(App application)
        {
            var services = new ServiceCollection()
              .AddSidekickConfiguration()
              .AddSidekickCoreServices()
              .AddSidekickBusinessServices()
              .AddSidekickLocalization()
              .AddSidekickUIServices()
              .AddSidekickUIWindows();

            services.AddSingleton(application);

            return services.BuildServiceProvider();
        }
    }
}
