using Microsoft.EntityFrameworkCore;
using BibliotecaApp.Models;

namespace BibliotecaApp.Data
{
    public class BibliotecaContext : DbContext
    {
        public BibliotecaContext(DbContextOptions<BibliotecaContext> options) : base(options)
        {
        }

        public DbSet<Livro> Livros { get; set; }
        public DbSet<Administrador> Administradores { get; set; }
    }
}
