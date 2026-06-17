using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Palpitao.Api.Controllers;

[ApiController]
[Route("admin/sentry")]
[Authorize(Roles = "Admin")]
public class AdminSentryController : ControllerBase
{
    private readonly IWebHostEnvironment _environment;

    public AdminSentryController(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    [HttpGet("test-error")]
    public IActionResult TestError()
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        throw new InvalidOperationException("Controlled Sentry test exception.");
    }
}
