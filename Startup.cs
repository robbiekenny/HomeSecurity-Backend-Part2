using Microsoft.Owin;
using Owin;

[assembly: OwinStartup(typeof(homesecurityserviceService.Startup))]

namespace homesecurityserviceService
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            ConfigureMobileApp(app);
        }
    }
}