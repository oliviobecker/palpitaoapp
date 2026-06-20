using Palpitao.Api.Services.Groups;

namespace Palpitao.Api.Tests.TestSupport;

/// <summary>
/// Test double for <see cref="IRequestGroupContext"/> that reports a fixed group id (or
/// null) without an HTTP context, so tests can exercise the AppDbContext multi-tenant
/// query filter and insert-stamping directly.
/// </summary>
public sealed class FakeRequestGroupContext : IRequestGroupContext
{
    public FakeRequestGroupContext(Guid? groupId) => CurrentGroupId = groupId;

    public Guid? CurrentGroupId { get; }
}
