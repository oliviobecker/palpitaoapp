namespace Palpitao.Infrastructure.Persistence.Seed;

internal static class Seeded
{
    /// <summary>Stable timestamp of every seeded row, so migrations stay deterministic.</summary>
    public static readonly DateTime At = new(2025, 7, 1, 0, 0, 0, DateTimeKind.Utc);
}
