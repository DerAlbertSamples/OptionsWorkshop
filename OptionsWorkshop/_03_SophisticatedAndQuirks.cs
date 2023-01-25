using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace OptionsWorkshop;

public class _03_Sophisticated : OptionsTests
{
    [Fact]
    public void ConfigureNamedServices_MustBeRegisteredAs_IConfigureOptions()
    {
        using (var sp = CreateTestServiceProvider(services =>
               {
                   services.AddTransient<IConfigureOptions<TestOptions>, ConfigureWithoutName>();
                   // correct registration
                   services.AddTransient<IConfigureOptions<TestOptions>, ConfigureNamed>();
               }))
        {
            var options = sp.GetRequiredService<IOptions<TestOptions>>().Value;

            options.Callstack.Should().Contain("ConfigureWithOutName_Configure");
            options.Callstack.Should().Contain("ConfigureNamed_Configure_for_");
            options.Callstack.Should().NotContain("ConfigureNamed_Configure");
        }

        using (var sp = CreateTestServiceProvider(services =>
               {
                   services.AddTransient<IConfigureOptions<TestOptions>, ConfigureWithoutName>();
                   // wrong registration, will not be called without a name
                   services.AddTransient<IConfigureNamedOptions<TestOptions>, ConfigureNamed>();
               }))
        {
            var options = sp.GetRequiredService<IOptions<TestOptions>>().Value;

            options.Callstack.Should().Contain("ConfigureWithOutName_Configure");
            options.Callstack.Should().NotContain("ConfigureNamed_Configure_for_");
            options.Callstack.Should().NotContain("ConfigureNamed_Configure");
        }
    }

    [Fact]
    public void ValidationException_Message_has_details()
    {
        var sp = CreateTestServiceProvider();
        var factory = sp.GetRequiredService<IOptionsFactory<TestOptions>>();

        var action = () => factory.Create("NotExistingForMissingUrlAndVat");


        action.Should().Throw<OptionsValidationException>()
            .WithMessage("*Missing Url on TestOptions*")
            .WithMessage("*Missing Vat on TestOptions for name 'NotExistingForMissingUrlAndVat'*");

        action.Should().Throw<OptionsValidationException>()
            .Which.Failures.Should().Contain("Missing Vat on TestOptions for name 'NotExistingForMissingUrlAndVat'");

        action.Should().Throw<OptionsValidationException>()
            .Which.Failures.Should().Contain("Missing Url on TestOptions for name 'NotExistingForMissingUrlAndVat'");

        action.Should().Throw<OptionsValidationException>()
            .Which.Failures
            .Where(failure => failure == "Missing Url on TestOptions for name 'NotExistingForMissingUrlAndVat'")
            .Should().HaveCount(2); // we have two validators for that registered, all will be called
    }

    [Fact]
    public void ConfigureNamedServices()
    {
        using var sp = CreateTestServiceProvider(services =>
        {
            services.AddTransient<IConfigureOptions<TestOptions>, ConfigureNamed>();
            services.AddTransient<IConfigureOptions<TestOptions>, ConfigureWithoutName>();

            services.AddOptions<TestOptions>("AName")
                .Configure(o =>
                {
                    o.Url = "blah";
                    o.Vat = 1m;
                });
        });

        var factory = sp.GetRequiredService<IOptionsFactory<TestOptions>>();
        var options = factory.Create("AName");


        options.Callstack.Should().NotContain("ConfigureWithOutName_Configure");
        options.Callstack.Should().Contain("ConfigureNamed_Configure_for_AName");
    }

    [Fact]
    public void IConfigureNamedOptions_Configure_WithoutName_WillNeverBeCalled()
    {
        using var sp = CreateTestServiceProvider(services =>
        {
            services.AddTransient<IConfigureOptions<TestOptions>, ConfigureNamed>();

            services.AddOptions<TestOptions>("AName")
                .Configure(o =>
                {
                    o.Url = "blah";
                    o.Vat = 1m;
                });
        });


        var options = sp.GetRequiredService<IOptions<TestOptions>>().Value;
        var factory = sp.GetRequiredService<IOptionsFactory<TestOptions>>();
        var monitor = sp.GetRequiredService<IOptionsMonitor<TestOptions>>();

        factory.Create("AName");
        monitor.Get("AName");
        monitor.Get(null);


        options.Callstack.Should().NotContain("ConfigureWithOutName_Configure"); // not registered
        options.Callstack.Should().NotContain("ConfigureNamed_Configure"); // never called
        options.Callstack.Should().Contain("ConfigureNamed_Configure_for_");
    }


    private ServiceProvider CreateTestServiceProvider(Action<IServiceCollection>? testServices = null)
    {
        return CreateServiceProvider((services, configuration) =>
        {
            services.AddOptions<TestOptions>()
                .Bind(configuration.GetSection("Options1"));

            services.AddTransient<IPostConfigureOptions<TestOptions>, PostConfigureTestOptions>();
            services.AddTransient<IValidateOptions<TestOptions>, ValidateTestOptions>();
            services.AddTransient<IValidateOptions<TestOptions>, MultipleValidateTestOptions>();
            testServices?.Invoke(services);
        });
    }

    internal class ConfigureWithoutName : IConfigureOptions<TestOptions>
    {
        public void Configure(TestOptions options)
        {
            options.Callstack.Add("ConfigureWithOutName_Configure");
        }
    }

    internal class ConfigureNamed : IConfigureNamedOptions<TestOptions>
    {
        public void Configure(TestOptions options)
        {
            options.Callstack.Add($"ConfigureNamed_Configure");
            throw new InvalidOperationException($"Will not be called from the framework in any case to my knowledge");
        }

        public void Configure(string? name, TestOptions options)
        {
            options.Callstack.Add($"ConfigureNamed_Configure_for_{name}");
        }
    }

    internal class PostConfigureTestOptions : IPostConfigureOptions<TestOptions>
    {
        private readonly MyService _myService;

        public PostConfigureTestOptions(MyService myService)
        {
            _myService = myService;
        }

        public void PostConfigure(string? name, TestOptions options)
        {
            options.Callstack.Add($"PostConfigureTestOptions_PostConfigure_{name}");
            options.Region = _myService.GetRegion();
        }
    }

    internal class ValidateTestOptions : IValidateOptions<TestOptions>
    {
        public ValidateOptionsResult Validate(string? name, TestOptions options)
        {
            options.Callstack.Add($"ValidateTestOptions_Validate_{name}");

            if (string.IsNullOrEmpty(options.Url))
            {
                return ValidateOptionsResult.Fail(
                    $"Missing {nameof(options.Url)} on {nameof(TestOptions)} for name '{name}'");
            }

            return ValidateOptionsResult.Success;
        }
    }

    internal class MultipleValidateTestOptions : IValidateOptions<TestOptions>
    {
        public ValidateOptionsResult Validate(string? name, TestOptions options)
        {
            options.Callstack.Add($"MultipleValidateTestOptions_Validate_{name}");

            var failures = new List<string>();
            if (string.IsNullOrEmpty(options.Url))
            {
                failures.Add($"Missing {nameof(options.Url)} on {nameof(TestOptions)} for name '{name}'");
            }

            if (options.Vat == 0m)
            {
                failures.Add($"Missing {nameof(options.Vat)} on {nameof(TestOptions)} for name '{name}'");
            }

            if (failures.Count == 0)
            {
                return ValidateOptionsResult.Success;
            }

            return ValidateOptionsResult.Fail(failures);
        }
    }
}