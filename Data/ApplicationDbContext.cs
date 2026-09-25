using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PlataformaCreditos.Models;

namespace PlataformaCreditos.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext(options)
{
    public DbSet<Cliente> Clientes { get; set; }
    public DbSet<SolicitudCredito> Solicitudes { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Garantizar que solo haya una solicitud pendiente por cliente a nivel de DB.
        // EstadoSolicitud.Pendiente = 0
        builder.Entity<SolicitudCredito>()
            .HasIndex(s => s.ClienteId)
            .HasFilter("[Estado] = 0")
            .IsUnique();
    }
}
