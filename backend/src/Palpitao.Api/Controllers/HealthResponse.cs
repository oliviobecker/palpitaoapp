using System.Text.Json.Serialization;

namespace Palpitao.Api.Controllers;

/// <summary>The body of the health endpoints; members a check does not report are left out.</summary>
/// <param name="Status"><c>ok</c>, <c>unavailable</c> or <c>migrations-pending</c>.</param>
/// <param name="Service">The process answering (liveness).</param>
/// <param name="Database">The database checked (readiness).</param>
/// <param name="Missing">OCR language codes whose models are missing.</param>
/// <param name="Languages">OCR language codes that are in place.</param>
public sealed record HealthResponse(
    string Status,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Service = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Database = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? Missing = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? Languages = null);
