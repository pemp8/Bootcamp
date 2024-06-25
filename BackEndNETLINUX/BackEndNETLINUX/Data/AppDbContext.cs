
using Microsoft.EntityFrameworkCore;
using BackEndNETLINUX.Models;


namespace BackEndNETLINUX.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<Trabajador> Trabajadores { get; set; }
        public DbSet<RegistroTiempo> RegistrosTiempo { get; set; }
    }
}
