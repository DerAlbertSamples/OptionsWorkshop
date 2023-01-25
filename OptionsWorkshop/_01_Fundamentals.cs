using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace OptionsWorkshop;

public class _01_Fundamentals : OptionsTests
{
    [Fact]
    public void Configuration_WithNoExistingSection()
    {
        using var sp = CreateServiceProvider((_, _) => { });

        var configuration = sp.GetRequiredService<IConfiguration>();

        var options = configuration.GetSection("OptionsNone").Get<TestOptions>();

        options.Should().BeNull();
    }

    [Fact]
    public void Configuration_WithExistingSections()
    {
        using var sp = CreateServiceProvider((_) => { });

        var configuration = sp.GetRequiredService<IConfiguration>();

        var options = configuration.GetSection("Options1").Get<TestOptions>()!; // can return null

        options.Region.Should().Be("TheRegion");
    }

    [Fact]
    public void Configuration_WithExistingSection_AndOverrideOptions()
    {
        using var sp = CreateServiceProvider((services, configuration) =>
        {
            services.AddOptions<TestOptions>()
                .Bind(configuration.GetSection("Options1"))
                .Configure(o => { o.Region = "NewRegion"; });
        });

        var configuration = sp.GetRequiredService<IConfiguration>();

        var options = configuration.GetSection("Options1").Get<TestOptions>()!;

        options.Region.Should()
            .Be("TheRegion"); // not the NewRegion, AddOptions() stuff is not used, also no validation
    }

    [Fact]
    public void IOptions_WithExistingSection_AndOverrideOptions()
    {
        using var sp = CreateServiceProvider((services, configuration) =>
        {
            services.AddOptions<TestOptions>()
                .Bind(configuration.GetSection("Options1"))
                .Configure(o => { o.Region = "NewRegion"; });
        });

        var options = sp.GetRequiredService<IOptions<TestOptions>>().Value;

        using var _ = new AssertionScope();

        options.Region.Should().Be("NewRegion"); // Region Updated
        options.Vat.Should().Be(0.19m); // but also read from configuration
    }

    [Fact]
    public void IOptions_WithExistingSection_AndOverrideOptions_OrderMatters()
    {
        using var sp = CreateServiceProvider((services, configuration) =>
        {
            services.AddOptions<TestOptions>()
                .Configure(o => { o.Region = "NewRegion"; })
                .Bind(configuration.GetSection("Options1"));
        });

        var options = sp.GetRequiredService<IOptions<TestOptions>>().Value;

        using var _ = new AssertionScope();

        options.Region.Should().Be("TheRegion"); // TheRegion comes from configuration
        options.Vat.Should().Be(0.19m); // but also read from configuration
    }

    [Fact]
    public void IOptions_WithoutExistingSection()
    {
        using var sp = CreateServiceProvider((services, configuration) =>
        {
            services.AddOptions<TestOptions>()
                .Bind(configuration.GetSection("OptionsNone"));
        });

        var options = sp.GetRequiredService<IOptions<TestOptions>>().Value; // No Null Handling Needed

        using var _ = new AssertionScope();

        options.Region.Should().Be("DefaultRegion"); // DefaultRegion
        options.Vat.Should().Be(0.0m); // but also read from configuration
    }

    [Fact]
    public void IOptions_MultipleAddOptions()
    {
        using var sp = CreateServiceProvider((services, configuration) =>
        {
            services.AddOptions<TestOptions>()
                .Bind(configuration.GetSection("Options1"));

            services.AddOptions<TestOptions>()
                .Configure(o => { o.Url = "NewUrl"; });

            services.AddOptions<TestOptions>()
                .Configure(o => { o.Vat = 0.22m; });
        });

        var options = sp.GetRequiredService<IOptions<TestOptions>>().Value; // No Null Handling Needed

        using var _ = new AssertionScope();

        options.Region.Should().Be("TheRegion");
        options.Url.Should().Be("NewUrl");
        options.Vat.Should().Be(0.22m);
    }
}