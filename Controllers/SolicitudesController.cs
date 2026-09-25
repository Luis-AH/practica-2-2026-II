using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using PlataformaCreditos.Data;
using PlataformaCreditos.Models;
using PlataformaCreditos.Models.ViewModels;
using PlataformaCreditos.Services;
using System.Text.Json;

namespace PlataformaCreditos.Controllers;

[Authorize]
public class SolicitudesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly IDistributedCache _cache;
    private readonly MensajeriaService _mensajeria;

    private const string SessionKeyUltimaSolicitud = "UltimaSolicitudMonto";

    public SolicitudesController(
        ApplicationDbContext context,
        UserManager<IdentityUser> userManager,
        IDistributedCache cache,
        MensajeriaService mensajeria)
    {
        _context = context;
        _userManager = userManager;
        _cache = cache;
        _mensajeria = mensajeria;
    }

    // Clave de caché por cliente
    private static string CacheKeySolicitudes(int clienteId) => $"solicitudes_cliente_{clienteId}";

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
            ModelState.AddModelError("MontoMinimo", "El monto mínimo no puede ser negativo.");
        if (filtro.MontoMaximo.HasValue && filtro.MontoMaximo < 0)
            ModelState.AddModelError("MontoMaximo", "El monto máximo no puede ser negativo.");
        if (filtro.MontoMinimo.HasValue && filtro.MontoMaximo.HasValue && filtro.MontoMinimo > filtro.MontoMaximo)
            ModelState.AddModelError("MontoMinimo", "El monto mínimo no puede ser mayor al máximo.");
        if (filtro.FechaDesde.HasValue && filtro.FechaHasta.HasValue && filtro.FechaDesde > filtro.FechaHasta)
            ModelState.AddModelError("FechaDesde", "La fecha inicial no puede ser posterior a la fecha final.");

        if (!ModelState.IsValid)
        {
            filtro.ErrorFiltro = string.Join(" | ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
            filtro.Solicitudes = new List<SolicitudCredito>();
            return View(filtro);
        }

        List<SolicitudCredito> todasLasSolicitudes;

        // --- Caché: Solo cachear cuando NO hay filtros activos ---
        bool hayFiltros = filtro.Estado.HasValue || filtro.MontoMinimo.HasValue ||
                          filtro.MontoMaximo.HasValue || filtro.FechaDesde.HasValue || filtro.FechaHasta.HasValue;

        if (!hayFiltros)
        {
            var cacheKey = CacheKeySolicitudes(cliente.Id);
            var cachedData = await _cache.GetStringAsync(cacheKey);

            if (cachedData != null)
            {
                todasLasSolicitudes = JsonSerializer.Deserialize<List<SolicitudCredito>>(cachedData)!;
            }
            else
            {
                todasLasSolicitudes = await _context.Solicitudes
                    .Where(s => s.ClienteId == cliente.Id)
                    .OrderByDescending(s => s.FechaSolicitud)
                    .ToListAsync();

                var cacheOptions = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60)
                };
                await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(todasLasSolicitudes), cacheOptions);
            }

            filtro.Solicitudes = todasLasSolicitudes;
        }
        else
        {
            // Con filtros: ir directo a la BD
            var query = _context.Solicitudes.Where(s => s.ClienteId == cliente.Id).AsQueryable();

            if (filtro.Estado.HasValue) query = query.Where(s => s.Estado == filtro.Estado.Value);
            if (filtro.MontoMinimo.HasValue) query = query.Where(s => s.MontoSolicitado >= filtro.MontoMinimo.Value);
            if (filtro.MontoMaximo.HasValue) query = query.Where(s => s.MontoSolicitado <= filtro.MontoMaximo.Value);
            if (filtro.FechaDesde.HasValue) query = query.Where(s => s.FechaSolicitud >= filtro.FechaDesde.Value);
            if (filtro.FechaHasta.HasValue) query = query.Where(s => s.FechaSolicitud <= filtro.FechaHasta.Value.AddDays(1));

            filtro.Solicitudes = await query.OrderByDescending(s => s.FechaSolicitud).ToListAsync();
        }

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

        // --- Sesión: Guardar la última solicitud visitada ---
        HttpContext.Session.SetString(SessionKeyUltimaSolicitud, $"{id}|{solicitud.MontoSolicitado:N2}|{solicitud.Estado}");

        return View(solicitud);
    }

    // GET: Solicitudes/Nueva
    public IActionResult Nueva()
    {
        return View(new NuevaSolicitudViewModel());
    }

    // POST: Solicitudes/Nueva
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Nueva(NuevaSolicitudViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var cliente = await _context.Clientes.FirstOrDefaultAsync(c => c.UsuarioId == user.Id);
        if (cliente == null)
        {
            ModelState.AddModelError("", "No se encontró el perfil de cliente asociado a tu cuenta.");
            return View(model);
        }

        if (!cliente.Activo)
        {
            ModelState.AddModelError("", "Tu perfil de cliente no está activo. No puedes solicitar créditos.");
            return View(model);
        }

        var tienePendiente = await _context.Solicitudes
            .AnyAsync(s => s.ClienteId == cliente.Id && s.Estado == EstadoSolicitud.Pendiente);

        if (tienePendiente)
        {
            ModelState.AddModelError("", "Ya tienes una solicitud en estado Pendiente. Debes esperar a que sea evaluada.");
            return View(model);
        }

        if (model.MontoSolicitado > cliente.IngresosMensuales * 10)
        {
            ModelState.AddModelError("MontoSolicitado", $"El monto no puede superar 10 veces tus ingresos mensuales (S/ {(cliente.IngresosMensuales * 10):N2}).");
            return View(model);
        }

        var nuevaSolicitud = new SolicitudCredito
        {
            ClienteId = cliente.Id,
            MontoSolicitado = model.MontoSolicitado,
            Estado = EstadoSolicitud.Pendiente,
            FechaSolicitud = DateTime.UtcNow
        };

        _context.Solicitudes.Add(nuevaSolicitud);
        await _context.SaveChangesAsync();

        // Invalidar caché al crear nueva solicitud
        await _cache.RemoveAsync(CacheKeySolicitudes(cliente.Id));

        // Publicar mensaje asíncrono a RabbitMQ (notificación de creación)
        await _mensajeria.PublicarSolicitudCreadaAsync(nuevaSolicitud.Id, user.Id, model.MontoSolicitado);

        TempData["SuccessMessage"] = "Tu solicitud de crédito ha sido registrada exitosamente y se encuentra en evaluación.";
        return RedirectToAction(nameof(MisSolicitudes));
    }
}
