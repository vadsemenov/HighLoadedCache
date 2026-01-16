using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace HighLoadedCache.Infrastructure;

public static class DiagnosticsConfig
{
    // Имя сервиса для идентификации в логах/трейсах
    public const string ServiceName = "HighLoadedCache";

    // Объект для создания трейсов (span'ов)
    public static readonly ActivitySource ActivitySource = new(ServiceName);

    // Объект для создания метрик (счетчиков, гистограмм)
    public static readonly Meter Meter = new(ServiceName);
}