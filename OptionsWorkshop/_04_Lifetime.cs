using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit.Abstractions;

namespace OptionsWorkshop;

public class _04_Lifetime : OptionsTests
{
    private readonly ITestOutputHelper _outputHelper;

    public _04_Lifetime(ITestOutputHelper outputHelper)
    {
        _outputHelper = outputHelper;
    }

    [Fact]
    public void IOptionOfT_Singleton()
    {
        using var sp = CreateServiceProvider();

        TestOptions options1, options2;

        using (var scope = sp.CreateScope())
        {
            options1 = scope.ServiceProvider.GetRequiredService<IOptions<TestOptions>>().Value;
        }

        using (var scope = sp.CreateScope())
        {
            options2 = scope.ServiceProvider.GetRequiredService<IOptions<TestOptions>>().Value;
        }

        using var _ = new AssertionScope();

        options1.Should().BeSameAs(options2);

        options1.Region.Should().Be("DefaultRegion");
        options2.Region.Should().Be("DefaultRegion");
    }

    [Fact]
    public void IOptionOfT_Singleton_EvenIfConfigChange()
    {
        using var sp = CreateServiceProvider((services, configuration) =>
        {
            services.AddOptions<TestOptions>()
                .Bind(configuration.GetSection("Options1"));
        });

        TestOptions scope1Options, scope2Options, rootScopeOptions;

        using (var scope = sp.CreateScope())
        {
            scope1Options = scope.ServiceProvider.GetRequiredService<IOptions<TestOptions>>().Value;
        }

        using (var scope = sp.CreateScope())
        {
            var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
            configuration["Options1:Region"] = "ChangedRegion";
            scope2Options = scope.ServiceProvider.GetRequiredService<IOptions<TestOptions>>().Value;
        }

        rootScopeOptions = sp.GetRequiredService<IOptions<TestOptions>>().Value;

        using var _ = new AssertionScope();

        scope1Options.Should().BeSameAs(scope2Options);
        scope1Options.Should().BeSameAs(rootScopeOptions);
        scope2Options.Should().BeSameAs(rootScopeOptions);

        scope1Options.Region.Should().Be("TheRegion");
        scope2Options.Region.Should().Be("TheRegion");
        rootScopeOptions.Region.Should().Be("TheRegion");
    }

    [Fact]
    public void IOptionSnapshotOfT_IsNotSingleton()
    {
        using var sp = CreateServiceProvider((services, configuration) =>
        {
            services.AddOptions<TestOptions>()
                .Bind(configuration.GetSection("Options1"));
        });

        TestOptions scope1Options, scope2Options;

        using (var scope = sp.CreateScope())
        {
            scope1Options = scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<TestOptions>>().Value;
        }

        using (var scope = sp.CreateScope())
        {
            scope2Options = scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<TestOptions>>().Value;
        }

        using var _ = new AssertionScope();

        scope1Options.Should().NotBeSameAs(scope2Options);

        scope1Options.Region.Should().Be("TheRegion");
        scope2Options.Region.Should().Be("TheRegion");
    }

    [Fact]
    public void IOptionSnapshotOfT_IsNotSingleton_ButUpdates()
    {
        var validateCount = 0;
        using var sp = CreateServiceProvider((services, configuration) =>
        {
            services.AddOptions<TestOptions>()
                .Bind(configuration.GetSection("Options1"))
                .Validate(_ =>
                {
                    validateCount++;
                    _outputHelper.WriteLine($"Validated Count is {validateCount}");
                    return true;
                });
        });

        TestOptions scope1Options, scope2Options, rootScopeOptions;

        using (var scope = sp.CreateScope())
        {
            scope1Options = scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<TestOptions>>().Value;
        }

        using (var scope = sp.CreateScope())
        {
            var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

            // configuration can updated via code
            configuration["Options1:Region"] = "ChangedRegion";

            scope2Options = scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<TestOptions>>().Value;
        }


        rootScopeOptions = sp.GetRequiredService<IOptionsSnapshot<TestOptions>>().Value;

        using var _ = new AssertionScope();

        scope1Options.Should().NotBeSameAs(scope2Options);
        scope1Options.Should().NotBeSameAs(rootScopeOptions);
        scope2Options.Should().NotBeSameAs(rootScopeOptions);

        scope1Options.Region.Should().Be("TheRegion");

        scope2Options.Region.Should().Be("ChangedRegion");
        rootScopeOptions.Region.Should().Be("ChangedRegion");

        validateCount.Should().Be(3);
    }

    [Fact]
    public void IOptionsOf_NamedOptions()
    {
        using var sp = CreateServiceProvider((services, configuration) =>
        {
            services.AddOptions<TestOptions>("Name1")
                .Bind(configuration.GetSection("Options1"));
            
            services.AddOptions<TestOptions>("Name2")
                .Bind(configuration.GetSection("Options2"));
        });

        var optionsFactory = sp.GetRequiredService<IOptionsSnapshot<TestOptions>>();

        var options1 = optionsFactory.Get("Name1");
        var options2 = optionsFactory.Get("Name2");

        using var _ = new AssertionScope();

        options1.Region.Should().Be("TheRegion");
        options2.Region.Should().Be("TheRegion2");
    }
    
    [Fact]
    public void IOptionsMonitorOfT_Get_DoesConfigureMultipleTimes()
    {
        var validateCount = 0;
        using var sp = CreateServiceProvider((services, configuration) =>
        {
            services.AddOptions<TestOptions>()
                .Bind(configuration.GetSection("Options1"))
                .Validate(_ =>
                {
                    validateCount++;
                    _outputHelper.WriteLine($"Validated Count is {validateCount}");
                    return true;
                });
        });

        var monitor = sp.GetRequiredService<IOptionsMonitor<TestOptions>>();

        var options = monitor.Get(null);
        var options2 = monitor.Get(string.Empty); // null or string.Empty does not differ for named options HERE

        options.Should().BeSameAs(options2);

        options.Region.Should().Be("TheRegion");

        validateCount.Should().Be(1);
    }

    [Fact]
    public void IOptionsMonitorOfT_CurrentValue_DoesNotConfigureMultipleTime()
    {
        var validateCount = 0;
        using var sp = CreateServiceProvider((services, configuration) =>
        {
            services.AddOptions<TestOptions>()
                .Bind(configuration.GetSection("Options1"))
                .Validate(_ =>
                {
                    validateCount++;
                    _outputHelper.WriteLine($"Validated Count is {validateCount}");
                    return true;
                });
        });

        var monitor = sp.GetRequiredService<IOptionsMonitor<TestOptions>>();

        var options = monitor.CurrentValue;
        options = monitor.CurrentValue;

        var options2 = monitor.Get(null);

        options.Should().BeSameAs(options2);

        options.Region.Should().Be("TheRegion");

        validateCount.Should().Be(1);
    }

    [Fact]
    public void IOptionsMonitorOfT_CurrentValue_InSameScope_DoesNotReceiveMultipleTime_OnSimpleChange()
    {
        var validateCount = 0;
        using var sp = CreateServiceProvider((services, configuration) =>
        {
            services.AddOptions<TestOptions>()
                .Bind(configuration.GetSection("Options1"))
                .Validate(_ =>
                {
                    validateCount++;
                    _outputHelper.WriteLine($"Validated Count is {validateCount}");
                    return true;
                });
        });

        var monitor = sp.GetRequiredService<IOptionsMonitor<TestOptions>>();

        var options = monitor.CurrentValue;
        using (var scope = sp.CreateScope())
        {
            var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

            // configuration can updated via code
            configuration["Options1:Region"] = "ChangedRegion";
        }

        options = monitor.CurrentValue;

        options.Region.Should().Be("TheRegion");

        validateCount.Should().Be(1);
    }

    [Fact]
    public void IOptionsMonitorOfT_CurrentValue_Updates_OnReload()
    {
        var validateCount = 0;
        using var sp = CreateServiceProvider((services, configuration) =>
        {
            services.AddOptions<TestOptions>()
                .Bind(configuration.GetSection("Options1"))
                .Validate(_ =>
                {
                    validateCount++;
                    _outputHelper.WriteLine($"Validated Count is {validateCount}");
                    return true;
                });
        });

        var monitor = sp.GetRequiredService<IOptionsMonitor<TestOptions>>();

        var options = monitor.CurrentValue;
        using (var scope = sp.CreateScope())
        {
            var configurationRoot = scope.ServiceProvider.GetRequiredService<IConfigurationRoot>();

            // configuration can updated via code
            configurationRoot["Options1:Region"] = "ChangedRegion";
            configurationRoot.Reload(); // needs to notify Monitor
        }

        options = monitor.CurrentValue;

        options.Region.Should().Be("ChangedRegion");

        validateCount.Should().Be(2);
    }

    [Fact]
    public void IOptionSnapshotOfT_CurrentValueIsTheSame_InScopes_IfNotChanged()
    {
        using var sp = CreateServiceProvider((services, configuration) =>
        {
            services.AddOptions<TestOptions>()
                .Bind(configuration.GetSection("Options1"));
        });

        TestOptions scope1Options, scope2Options;

        using (var scope = sp.CreateScope())
        {
            scope1Options = scope.ServiceProvider.GetRequiredService<IOptionsMonitor<TestOptions>>().CurrentValue;
        }

        using (var scope = sp.CreateScope())
        {
            scope2Options = scope.ServiceProvider.GetRequiredService<IOptionsMonitor<TestOptions>>().CurrentValue;
        }

        using var _ = new AssertionScope();

        scope1Options.Should().BeSameAs(scope2Options);

        scope1Options.Region.Should().Be("TheRegion");
        scope2Options.Region.Should().Be("TheRegion");
    }

    [Fact]
    public void IOptionsMonitorOfT_ServiceIsSingleton_InScopes()
    {
        var validateCount = 0;
        using var sp = CreateServiceProvider((services, configuration) =>
        {
            services.AddOptions<TestOptions>()
                .Bind(configuration.GetSection("Options1"))
                .Validate(o =>
                {
                    validateCount++;
                    return true;
                });
        });

        IOptionsMonitor<TestOptions> scope1Service, scope2Service;


        using (var scope = sp.CreateScope())
        {
            scope1Service = scope.ServiceProvider.GetRequiredService<IOptionsMonitor<TestOptions>>();
            var options = scope1Service.CurrentValue;
            options = scope1Service.Get(null);
        }

        using (var scope = sp.CreateScope())
        {
            scope2Service = scope.ServiceProvider.GetRequiredService<IOptionsMonitor<TestOptions>>();
            var options = scope1Service.CurrentValue;
            options = scope1Service.Get(null);
        }

        using var _ = new AssertionScope();

        scope1Service.Should().BeSameAs(scope2Service);

        validateCount.Should().Be(1);
    }

    [Fact]
    public void IOptionsMonitorOfT_CurrentValue_Notifies_OnReload()
    {
        var validateCount = 0;
        using var sp = CreateServiceProvider((services, configuration) =>
        {
            services.AddOptions<TestOptions>()
                .Bind(configuration.GetSection("Options1"))
                .Validate(_ =>
                {
                    validateCount++;
                    _outputHelper.WriteLine($"Validated Count is {validateCount}");
                    return true;
                });
        });

        TestOptions currentOptions, onChangeOptions = new();

        var monitor = sp.GetRequiredService<IOptionsMonitor<TestOptions>>();
        monitor.OnChange((newOptions, name) => { onChangeOptions = newOptions; });

        currentOptions = monitor.CurrentValue;
        using (var scope = sp.CreateScope())
        {
            var configurationRoot = scope.ServiceProvider.GetRequiredService<IConfigurationRoot>();

            // configuration can updated via code
            // but this does not notify about changes
            configurationRoot["Options1:Region"] = "ChangedRegion";

            // needs to notify IOptionMonitor<T>, the ConfigurationProvider does this if needed
            configurationRoot.Reload();
        }

        currentOptions.Region.Should().Be("TheRegion");

        onChangeOptions.Region.Should().Be("ChangedRegion");

        validateCount.Should().Be(2);
    }
}