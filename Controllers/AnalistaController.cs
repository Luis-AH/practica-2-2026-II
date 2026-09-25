using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlataformaCreditos.Data;
using PlataformaCreditos.Models;
using System.ComponentModel.DataAnnotations;

using Microsoft.Extensions.Caching.Distributed;
using PlataformaCreditos.Services;

namespace PlataformaCreditos.Controllers;

[Authorize(Roles = "Analista")]
public class AnalistaController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly NotificacionesService _notificacionesService;
    private readonly IDistributedCache _cache;

    public AnalistaController(ApplicationDbContext context, NotificacionesService notificacionesService, IDistributedCache cache)
    {
        _context = context;
        _notificacionesService = notificacionesService;
        _cache = cache;
    }

    // GET: /Analista
    public async Task<IActionResult> Index()
    {
        var solicitudesPendientes = await _context.Solicitudes
            .Include(s => s.Cliente)
            .ThenInclude(c => c!.Usuario)
            .Where(s => s.Estado == EstadoSolicitud.Pendiente)
            .OrderBy(s => s.FechaSolicitud)
            .ToListAsync();

        return View(solicitudesPendientes);
    }

    // POST: /Analista/Aprobar/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Aprobar(int id)
    {
        var solicitud = await _context.Solicitudes
            .Include(s => s.Cliente)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (solicitud == null) return NotFound();

        if (solicitud.Estado != EstadoSolicitud.Pendiente)
        {
            TempData["ErrorMessage"] = "Esta solicitud ya fue procesada.";
            return RedirectToAction(nameof(Index));
        }

        // Regla: No aprobar si el monto excede 5 veces los ingresos
        if (solicitud.MontoSolicitado > solicitud.Cliente!.IngresosMensuales * 5)
        {
            TempData["ErrorMessage"] = $"No se puede aprobar. El monto solicitado (S/ {solicitud.MontoSolicitado:N2}) excede 5 veces los ingresos mensuales del cliente (S/ {solicitud.Cliente.IngresosMensuales * 5:N2}).";
            return RedirectToAction(nameof(Index));
        }

        solicitud.Estado = EstadoSolicitud.Aprobado;
        await _context.SaveChangesAsync();

        // Invalidar caché
        await _cache.RemoveAsync($"solicitudes_cliente_{solicitud.ClienteId}");

        // Notificar por WebSocket al cliente
        if (solicitud.Cliente?.UsuarioId != null)
        {
            await _notificacionesService.EnviarEstadoActualizadoAsync(
                solicitud.Cliente.UsuarioId, solicitud.Id, "Aprobado", "");
        }

        TempData["SuccessMessage"] = $"La solicitud #{solicitud.Id} ha sido APROBADA exitosamente.";
        return RedirectToAction(nameof(Index));
    }

    // POST: /Analista/Rechazar
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Rechazar(int id, string motivo)
    {
        if (string.IsNullOrWhiteSpace(motivo))
        {
            TempData["ErrorMessage"] = "El motivo de rechazo es obligatorio.";
            return RedirectToAction(nameof(Index));
        }

        var solicitud = await _context.Solicitudes
            .Include(s => s.Cliente)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (solicitud == null) return NotFound();

        if (solicitud.Estado != EstadoSolicitud.Pendiente)
        {
            TempData["ErrorMessage"] = "Esta solicitud ya fue procesada.";
            return RedirectToAction(nameof(Index));
        }

        solicitud.Estado = EstadoSolicitud.Rechazado;
        solicitud.MotivoRechazo = motivo;
        
        await _context.SaveChangesAsync();

        // Invalidar caché
        await _cache.RemoveAsync($"solicitudes_cliente_{solicitud.ClienteId}");

        // Notificar por WebSocket al cliente
        if (solicitud.Cliente?.UsuarioId != null)
        {
            await _notificacionesService.EnviarEstadoActualizadoAsync(
                solicitud.Cliente.UsuarioId, solicitud.Id, "Rechazado", motivo);
        }

        TempData["SuccessMessage"] = $"La solicitud #{solicitud.Id} ha sido RECHAZADA.";
        return RedirectToAction(nameof(Index));
    }
}
