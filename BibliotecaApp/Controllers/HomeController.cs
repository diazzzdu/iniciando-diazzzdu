using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using BibliotecaApp.Models;
using BibliotecaApp.Data;
using System.Linq;

namespace BibliotecaApp.Controllers
{
    public class HomeController : Controller
    {
        private readonly BibliotecaContext _context;

        public HomeController(BibliotecaContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Sobre()
        {
            return View();
        }

        public IActionResult Produtos()
        {
            var livros = _context.Livros.ToList();
            return View(livros);
        }

        public IActionResult Contato()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
