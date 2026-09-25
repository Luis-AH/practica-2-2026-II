using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PlataformaCreditos.Models;

namespace PlataformaCreditos.Data;

public static class DataSeeder
{
    public static async Task SeedDataAsync(IServiceProvider serviceProvider)
    {
        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = serviceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var context = serviceProvider.GetRequiredService<ApplicationDbContext>();

        // Ensure Database is created
        await context.Database.MigrateAsync();

        // 1. Create Roles
        string[] roleNames = { "Analista", "Cliente" };
        foreach (var roleName in roleNames)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new IdentityRole(roleName));
            }
        }

        // 2. Create Analyst User
        var analistaEmail = "analista@banco.com";
        var analistaUser = await userManager.FindByEmailAsync(analistaEmail);
        if (analistaUser == null)
        {
            analistaUser = new IdentityUser { Id = "analista-fixed-id", UserName = analistaEmail, Email = analistaEmail, EmailConfirmed = true };
            await userManager.CreateAsync(analistaUser, "Password123!");
            await userManager.AddToRoleAsync(analistaUser, "Analista");
        }

        // 3. Create 2 Clients
        var cliente1Email = "cliente1@banco.com";
        var cliente1User = await userManager.FindByEmailAsync(cliente1Email);
        if (cliente1User == null)
        {
            cliente1User = new IdentityUser { Id = "cliente1-fixed-id", UserName = cliente1Email, Email = cliente1Email, EmailConfirmed = true };
            await userManager.CreateAsync(cliente1User, "Password123!");
            await userManager.AddToRoleAsync(cliente1User, "Cliente");
        }

        var cliente2Email = "cliente2@banco.com";
        var cliente2User = await userManager.FindByEmailAsync(cliente2Email);
        if (cliente2User == null)
        {
            cliente2User = new IdentityUser { Id = "cliente2-fixed-id", UserName = cliente2Email, Email = cliente2Email, EmailConfirmed = true };
            await userManager.CreateAsync(cliente2User, "Password123!");
            await userManager.AddToRoleAsync(cliente2User, "Cliente");
        }

        // 4. Create Domain Models (Clientes)
        if (!context.Clientes.Any())
        {
            var c1 = new Cliente { UsuarioId = cliente1User.Id, IngresosMensuales = 5000m, Activo = true };
            var c2 = new Cliente { UsuarioId = cliente2User.Id, IngresosMensuales = 8000m, Activo = true };
            
            context.Clientes.AddRange(c1, c2);
            await context.SaveChangesAsync();

            // 5. Create 2 Solicitudes (1 Pendiente for c1, 1 Aprobada for c2)
            var s1 = new SolicitudCredito
            {
                ClienteId = c1.Id,
                MontoSolicitado = 10000m,
                Estado = EstadoSolicitud.Pendiente,
                FechaSolicitud = DateTime.UtcNow
            };

            var s2 = new SolicitudCredito
            {
                ClienteId = c2.Id,
                MontoSolicitado = 20000m,
                Estado = EstadoSolicitud.Aprobado,
                FechaSolicitud = DateTime.UtcNow.AddDays(-2)
            };

            context.Solicitudes.AddRange(s1, s2);
            await context.SaveChangesAsync();
        }
    }
}
