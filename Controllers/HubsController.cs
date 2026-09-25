using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PlataformaCreditos.Services;

namespace PlataformaCreditos.Controllers;

[Authorize]
[Route("hubs/solicitudes")]
public class HubsController : Controller
{
    private readonly NotificacionesService _notificacionesService;
    private readonly UserManager<IdentityUser> _userManager;

    public HubsController(NotificacionesService notificacionesService, UserManager<IdentityUser> userManager)
    {
        _notificacionesService = notificacionesService;
        _userManager = userManager;
    }

    [HttpPost]
    public async Task<IActionResult> Auth([FromForm] string socket_id, [FromForm] string channel_name)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        // Validar que el canal privado corresponde a este usuario
        var expectedChannel = $"private-solicitudes-{user.Id}";
        if (channel_name != expectedChannel)
        {
            return Forbid();
        }

        try
        {
            var auth = _notificacionesService.AutenticarConexion(channel_name, socket_id);
            return Json(auth);
        }
        catch (Exception)
        {
            return StatusCode(500, "Error al autenticar con PieSocket");
        }
    }
}
