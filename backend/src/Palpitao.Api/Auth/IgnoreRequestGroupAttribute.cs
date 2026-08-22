namespace Palpitao.Api.Auth;

/// <summary>
/// Marks an endpoint whose tenant comes from the route itself (a season public key), not
/// from the <c>X-Group-Id</c> header. On such endpoints <see cref="Services.Groups.RequestGroupContext"/>
/// reports no request group, so a stray header — a logged-in browser sends one on every
/// request — cannot scope, and therefore cannot hide, data the caller is entitled to see.
/// </summary>
/// <remarks>
/// This disables only the defence-in-depth query filter, never an access check: endpoints
/// carrying it must still scope every query explicitly to the tenant they resolved from the
/// route, because with no request group the global filter matches all groups.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class IgnoreRequestGroupAttribute : Attribute;
