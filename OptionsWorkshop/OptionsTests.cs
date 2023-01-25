using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace OptionsWorkshop;

public abstract class OptionsTests
{
    public ServiceProvider CreateServiceProvider(Action<IServiceCollection, IConfiguration> builderAction)
    {
        var services = new ServiceCollection();
        var configurationRoot = CreateConfiguration();
        services.AddSingleton(configurationRoot);
        services.AddSingleton<IConfiguration>(sp => sp.GetRequiredService<IConfigurationRoot>());
        services.AddTransient<MyService>();
        services.AddOptions(); // only needed when no AddOptions<T>() is called in the Application
        builderAction(services, configurationRoot);
        return services.BuildServiceProvider();
    }
    
    public ServiceProvider CreateServiceProvider(Action<IServiceCollection> builderAction)
    {
        return CreateServiceProvider((services, _) => builderAction(services));
    }
    
    public ServiceProvider CreateServiceProvider()
    {
        return CreateServiceProvider((_, _) => { });
    }
    
    public IConfigurationRoot CreateConfiguration()
    {
        return CreateConfiguration(builder =>
        {
            builder.AddInMemoryCollection(new KeyValuePair<string, string?>[]
            {
                new("Options1:Region", "TheRegion"),
                new("Options1:Url", "TheUrl"),
                new("Options1:Vat", "0.19"),
                new("Options1:Enabled", "false"),
                new("Options2:Region", "TheRegion2"),
                new("Options2:Url", "TheUrl2"),
                new("Options2:Vat", "0.7"),
                new("Options2:Enabled", "true")
            });
        });
    }

    public IConfigurationRoot CreateConfiguration(Action<IConfigurationBuilder> builderAction)
    {
        var builder = new ConfigurationManager(); // new in .NET 6, in .NET 5//is was ConfigurationBuilder()
        builderAction(builder);
        return builder;
    }
}