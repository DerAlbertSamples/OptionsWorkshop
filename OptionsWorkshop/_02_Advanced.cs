using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace OptionsWorkshop;

public class _02_Advanced : OptionsTests
{
    [Fact]
    public void Options_NamedOptions()
    {
        using var sp = CreateServiceProvider((services, configuration) =>
        {
            services.AddOptions<TestOptions>("Name1")
                .Bind(configuration.GetSection("Options1"));
            
            services.AddOptions<TestOptions>("Name2")
                .Bind(configuration.GetSection("Options2"));
        });

        var optionsFactory = sp.GetRequiredService<IOptionsFactory<TestOptions>>();

        var options1 = optionsFactory.Create("Name1");
        var options2 = optionsFactory.Create("Name2");

        using var _ = new AssertionScope();

        options1.Region.Should().Be("TheRegion");
        options2.Region.Should().Be("TheRegion2");
    }

    [Fact]
    public void Options_PostConfigure_ToFixupStuff()
    {
        using var sp = CreateServiceProvider(services =>
        {
            services.AddOptions<TestOptions>()
                .PostConfigure(o =>
                {
                    if (string.IsNullOrEmpty(o.Url))
                    {
                        o.Url = "http://posturl";
                    }
                });
        });

        var options = sp.GetRequiredService<IOptions<TestOptions>>().Value;

        options.Url.Should().Be("http://posturl");
    }

    [Fact]
    public void Options_PostConfigure()
    {
        using var sp = CreateServiceProvider(services =>
        {
            services.AddOptions<TestOptions>()
                .PostConfigure(o =>
                {
                    if (string.IsNullOrEmpty(o.Url))
                    {
                        o.Url = "http://posturl";
                    }
                })
                .Configure(o => { o.Url = "http://configureurl"; });
        });

        var options = sp.GetRequiredService<IOptions<TestOptions>>().Value;

        options.Url.Should().Be("http://configureurl");
    }

    [Fact]
    public void Options_Validate()
    {
        using var sp = CreateServiceProvider(services =>
        {
            services.AddOptions<TestOptions>()
                // do not validate with this method, use IValidateOptions<T> instead,
                // because you can specify error messages
                .Validate(o =>
                {
                    if (string.IsNullOrEmpty(o.Url))
                    {
                        return false;
                    }

                    return true;
                });
        });

        var optionsAccessor = sp.GetRequiredService<IOptions<TestOptions>>();

        var action = () => optionsAccessor.Value;

        action.Should().Throw<OptionsValidationException>().WithMessage("*validation error*");
    }

    [Fact]
    public void Options_WithServiceDependencies()
    {
        using var sp = CreateServiceProvider(services =>
        {
            services.AddOptions<TestOptions>()
                .Configure<MyService>((o, myService) => { o.Region = myService.GetRegion(); });
        });

        var options = sp.GetRequiredService<IOptions<TestOptions>>().Value;

        options.Region.Should().Be("MyServiceRegion");
    }

    [Fact]
    public void Options_WithServiceDependencies_OtherOptions()
    {
        using var sp = CreateServiceProvider(services =>
        {
            services.AddOptions<RegionOptions>()
                .Configure(o => o.Region = "OtherRegion");

            services.AddOptions<TestOptions>()
                .Configure<IOptions<RegionOptions>>(
                    (o, regionOptionsAccessor) => { o.Region = regionOptionsAccessor.Value.Region; });
        });

        var options = sp.GetRequiredService<IOptions<TestOptions>>().Value;

        options.Region.Should().Be("OtherRegion");
    }

    [Fact]
    public void Options_WithServiceDependencies_Multiple()
    {
        using var sp = CreateServiceProvider(services =>
        {
            services.AddOptions<RegionOptions>()
                .Configure(o => o.Region = "OtherRegion");

            services.AddOptions<TestOptions>()
                .Configure<IOptions<RegionOptions>, MyService>(
                    (o, regionOptionsAccessor, myService) =>
                    {
                        o.Region = $"{myService.GetRegion()}_{regionOptionsAccessor.Value.Region}";
                    });
        });

        var options = sp.GetRequiredService<IOptions<TestOptions>>().Value;

        options.Region.Should().Be("MyServiceRegion_OtherRegion");
    }
}