using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Palpitao.Api.Auth;
using Palpitao.Api.Controllers;
using Palpitao.Api.Services.Groups;
using Xunit;

namespace Palpitao.Api.Tests.Standings;

/// <summary>
/// Guards the wiring the public link depends on, which unit tests of the service cannot see:
/// the endpoint really is anonymous, it is not accidentally subject to the group action
/// filters (which <c>[AllowAnonymous]</c> would not switch off), and a stray
/// <c>X-Group-Id</c> from a logged-in browser cannot scope it.
/// </summary>
public class PublicStandingsEndpointTests
{
    private static T? Attr<T>() where T : Attribute
        => (T?)Attribute.GetCustomAttribute(typeof(PublicStandingsController), typeof(T));

    [Fact]
    public void The_public_controller_is_anonymous()
        => Assert.NotNull(Attr<AllowAnonymousAttribute>());

    [Fact]
    public void The_public_controller_carries_no_group_action_filters()
    {
        // These are action filters, not authorization filters: adding one here would run and
        // return 403 for every anonymous caller, no matter that the endpoint allows anonymous.
        Assert.Null(Attr<RequireGroupParticipantAttribute>());
        Assert.Null(Attr<RequireGroupAdminAttribute>());
    }

    [Fact]
    public void The_public_controller_is_rate_limited_and_ignores_the_request_group()
    {
        Assert.Equal("public", Attr<EnableRateLimitingAttribute>()?.PolicyName);
        Assert.NotNull(Attr<IgnoreRequestGroupAttribute>());
    }

    [Fact]
    public void The_public_routes_are_under_the_public_prefix()
        => Assert.Equal("public/seasons", Attr<RouteAttribute>()?.Template);

    // -----------------------------------------------------------------------
    // RequestGroupContext honours the marker
    // -----------------------------------------------------------------------

    private static RequestGroupContext ContextWith(Guid headerGroup, bool marked)
    {
        var http = new DefaultHttpContext();
        http.Request.Headers[CurrentGroupService.GroupHeader] = headerGroup.ToString();
        if (marked)
        {
            http.SetEndpoint(new Endpoint(
                _ => Task.CompletedTask,
                new EndpointMetadataCollection(new IgnoreRequestGroupAttribute()),
                "public"));
        }

        return new RequestGroupContext(new HttpContextAccessor { HttpContext = http });
    }

    [Fact]
    public void A_marked_endpoint_reports_no_request_group_despite_the_header()
    {
        // Otherwise an admin of group A opening group B's public link would filter B's season
        // away and get a spurious 404.
        Assert.Null(ContextWith(Guid.NewGuid(), marked: true).CurrentGroupId);
    }

    [Fact]
    public void An_unmarked_endpoint_still_reads_the_header()
    {
        var groupId = Guid.NewGuid();

        Assert.Equal(groupId, ContextWith(groupId, marked: false).CurrentGroupId);
    }
}
