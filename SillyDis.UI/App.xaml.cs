using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using SillyDis.Core.Services;
using SillyDis.UI.ViewModels;

namespace SillyDis.UI
{
    public partial class App : Application
    {
        public new static App Current => (App)Application.Current;
        public IServiceProvider Services { get; }

        public App()
        {
            Services = ConfigureServices();
        }

        private static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            // Core services
            services.AddSingleton<ProfileManager>();
            services.AddSingleton<IUdpNetworkService, UdpNetworkService>();

            // ViewModels
            services.AddTransient<MainViewModel>();

            return services.BuildServiceProvider();
        }
    }
}
