using OpenTelemetry.Exporter;

namespace Friday.API.Configuration;

public sealed class OpenTelemetryOptions
{
    public const string SectionName = "OpenTelemetry";

    /// <summary>When false, OpenTelemetry SDK is not registered (host still creates <see cref="System.Diagnostics.Activity"/> for requests).</summary>
    public bool Enabled { get; set; } = true;

    public string ServiceName { get; set; } = "Friday.API";

    /// <summary>Optional OTLP endpoint, e.g. <c>http://localhost:4317</c> (gRPC) or <c>http://localhost:4318</c> (HTTP/protobuf). Empty = no OTLP exporter.</summary>
    public string? OtlpEndpoint { get; set; }

    /// <summary>
    /// <c>Grpc</c> (default; khớp Jaeger <c>:4317</c>) hoặc <c>HttpProtobuf</c> (OTLP/HTTP, thường <c>:4318</c>; cần collector hỗ trợ đúng path).
    /// </summary>
    public string? OtlpProtocol { get; set; }

    internal OtlpExportProtocol ResolveOtlpProtocol()
    {
        if (
            !string.IsNullOrWhiteSpace(OtlpProtocol)
            && string.Equals(OtlpProtocol, "HttpProtobuf", StringComparison.OrdinalIgnoreCase)
        )
        {
            return OtlpExportProtocol.HttpProtobuf;
        }

        return OtlpExportProtocol.Grpc;
    }

    /// <summary>
    /// <see cref="LogExportModes.Serilog"/>: chỉ dùng sinks trong <c>SerilogClassic</c> (Console, file JSON, v.v.). Không ghi file rồi mới gửi OTLP; log và trace/metric OTLP là hai đường độc lập (trace qua SDK OpenTelemetry, không qua Serilog trừ khi bật sink OTLP bên dưới).
    /// <see cref="LogExportModes.OpenTelemetry"/>: Serilog chỉ gửi log qua sink OTLP khi <see cref="OtlpEndpoint"/> hợp lệ; không dùng SerilogClassic. Nếu endpoint thiếu/sai thì fallback về SerilogClassic.
    /// /// </summary>
    public string LogExport { get; set; } = LogExportModes.Serilog;
}

public static class LogExportModes
{
    public const string Serilog = "Serilog";
    public const string OpenTelemetry = "OpenTelemetry";
}
