using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CrystalGroupHome.Internal.Common.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HomeController : Controller
    {
        [HttpGet("health")]
        [AllowAnonymous]
        public IActionResult GetHealth()
        {
            return Ok(new { status = "running", timestamp = DateTime.UtcNow });
        }

        [HttpGet("user")]
        [Authorize]
        public IActionResult GetCurrentUser()
        {
            var user = HttpContext.User;

            return Ok(new
            {
                username = user.Identity?.Name,
                isAuthenticated = user.Identity?.IsAuthenticated ?? false,
                authenticationType = user.Identity?.AuthenticationType,
                claims = user.Claims.Select(c => new { type = c.Type, value = c.Value }).ToList(),
                timestamp = DateTime.UtcNow
            });
        }
    }
}
