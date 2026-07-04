using BibliotecaApp.Data;
using Microsoft.AspNetCore.Mvc;
using System.Linq;

namespace BibliotecaApp.Controllers
{
    public class ContaController : Controller
    {
        private readonly BibliotecaContext _context;

        public ContaController(BibliotecaContext context)
        {
            _context = context;
        }

        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Login(string email, string senha)
        {
            var admin = _context.Administradores.FirstOrDefault(a => a.Email == email && a.Senha == senha);

            if (admin == null)
            {
                ViewBag.Erro = "E-mail ou senha inválidos.";
                return View();
            }

            HttpContext.Session.SetString("AdminEmail", admin.Email);
            HttpContext.Session.SetString("AdminNome", admin.Nome);

            return RedirectToAction("Index", "Livros");
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }
    }
}
