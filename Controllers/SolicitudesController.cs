using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlataformaCreditos.Data;
using PlataformaCreditos.Models;
using PlataformaCreditos.Models.ViewModels;

namespace PlataformaCreditos.Controllers;

[Authorize]
public class SolicitudesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<IdentityUser> _userManager;

    public SolicitudesController(ApplicationDbContext context, UserManager<IdentityUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    // GET: Solicitudes/MisSolicitudes
    public async Task<IActionResult> MisSolicitudes(SolicitudFiltroViewModel filtro)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var cliente = await _context.Clientes
            .FirstOrDefaultAsync(c => c.UsuarioId == user.Id);

        if (cliente == null)
        {
            return View(new SolicitudFiltroViewModel
            {
                ErrorFiltro = "No se encontró un perfil de cliente asociado a tu cuenta."
            });
        }

        // --- Validaciones de filtros server-side ---
        if (filtro.MontoMinimo.HasValue && filtro.MontoMinimo < 0)
        {
            ModelState.AddModelError("MontoMinimo", "El monto mínimo no puede ser negativo.");
        }
        if (filtro.MontoMaximo.HasValue && filtro.MontoMaximo < 0)
        {
            ModelState.AddModelError("MontoMaximo", "El monto máximo no puede ser negativo.");
        }
        if (filtro.MontoMinimo.HasValue && filtro.MontoMaximo.HasValue
            && filtro.MontoMinimo > filtro.MontoMaximo)
        {
            ModelState.AddModelError("MontoMinimo", "El monto mínimo no puede ser mayor al máximo.");
        }
        if (filtro.FechaDesde.HasValue && filtro.FechaHasta.HasValue
            && filtro.FechaDesde > filtro.FechaHasta)
        {
            ModelState.AddModelError("FechaDesde", "La fecha inicial no puede ser posterior a la fecha final.");
        }

        if (!ModelState.IsValid)
        {
            filtro.ErrorFiltro = string.Join(" | ", ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage));
            filtro.Solicitudes = new List<SolicitudCredito>();
            return View(filtro);
        }

        // --- Construcción del query con filtros ---
        var query = _context.Solicitudes
            .Where(s => s.ClienteId == cliente.Id)
            .AsQueryable();

        if (filtro.Estado.HasValue)
            query = query.Where(s => s.Estado == filtro.Estado.Value);

        if (filtro.MontoMinimo.HasValue)
            query = query.Where(s => s.MontoSolicitado >= filtro.MontoMinimo.Value);

        if (filtro.MontoMaximo.HasValue)
            query = query.Where(s => s.MontoSolicitado <= filtro.MontoMaximo.Value);

        if (filtro.FechaDesde.HasValue)
            query = query.Where(s => s.FechaSolicitud >= filtro.FechaDesde.Value);

        if (filtro.FechaHasta.HasValue)
            query = query.Where(s => s.FechaSolicitud <= filtro.FechaHasta.Value.AddDays(1));

        filtro.Solicitudes = await query
            .OrderByDescending(s => s.FechaSolicitud)
            .ToListAsync();

        return View(filtro);
    }

    // GET: Solicitudes/Detalle/5
    public async Task<IActionResult> Detalle(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var cliente = await _context.Clientes
            .FirstOrDefaultAsync(c => c.UsuarioId == user.Id);

        if (cliente == null) return NotFound();

        var solicitud = await _context.Solicitudes
            .Include(s => s.Cliente)
            .FirstOrDefaultAsync(s => s.Id == id && s.ClienteId == cliente.Id);

        if (solicitud == null) return NotFound();

        return View(solicitud);
    }
}
