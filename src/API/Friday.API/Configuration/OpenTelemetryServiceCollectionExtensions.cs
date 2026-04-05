using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Friday.API.Configuration;

public static class OpenTelemetryServiceCollectionExtensions
{
    public static IServiceCollection AddFridayOpenTelemetry(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        OpenTelemetryOptions options =
            configuration.GetSection(OpenTelemetryOptions.SectionName).Get<OpenTelemetryOptions>()
            ?? new OpenTelemetryOptions();

        if (!options.Enabled)
        {
            return services;
        }

        Uri? otlpUri = null;
        bool exportOtlp =
            !string.IsNullOrWhiteSpace(options.OtlpEndpoint)
            && Uri.TryCreate(options.OtlpEndpoint, UriKind.Absolute, out otlpUri);

        OtlpExportProtocol protocol = options.ResolveOtlpProtocol();

        var openTelemetryBuilder = services
            .AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(options.ServiceName));

        openTelemetryBuilder.WithTracing(t =>
        {
            t.SetSampler(new AlwaysOnSampler())
                .AddAspNetCoreInstrumentation(o => o.RecordException = true)
                .AddHttpClientInstrumentation();

            if (exportOtlp && otlpUri is not null)
            {
                Uri endpoint = otlpUri;
                t.AddOtlpExporter(o =>
                {
                    o.Endpoint = endpoint;
                    o.Protocol = protocol;
                    o.ExportProcessorType = ExportProcessorType.Simple;
                });
            }
        });

        openTelemetryBuilder.WithMetrics(m =>
        {
            m.AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation();

            if (exportOtlp && otlpUri is not null)
            {
                Uri endpoint = otlpUri;
                m.AddOtlpExporter(o =>
                {
                    o.Endpoint = endpoint;
                    o.Protocol = protocol;
                });
            }
        });

        return services;
    }
}
