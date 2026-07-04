using BibliotecaApp.Data;
using BibliotecaApp.Filters;
using BibliotecaApp.Models;
using Microsoft.AspNetCore.Mvc;
using System.Linq;

namespace BibliotecaApp.Controllers
{
    [AdminOnly]
    public class LivrosController : Controller
    {
        private readonly BibliotecaContext _context;

        public LivrosController(BibliotecaContext context)
        {
            _context = context;
        }

        public IActionResult Index(string busca)
        {
            var livros = _context.Livros.AsQueryable();

            if (!string.IsNullOrEmpty(busca))
            {
                livros = livros.Where(l => l.Titulo.Contains(busca) || l.Autor.Contains(busca));
            }

            ViewBag.Busca = busca;
            return View(livros.ToList());
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Create(Livro livro)
        {
            if (ModelState.IsValid)
            {
                _context.Livros.Add(livro);
                _context.SaveChanges();
                return RedirectToAction(nameof(Index));
            }
            return View(livro);
        }

        public IActionResult Edit(int id)
        {
            var livro = _context.Livros.Find(id);
            if (livro == null) return NotFound();
            return View(livro);
        }

        [HttpPost]
        public IActionResult Edit(int id, Livro livro)
        {
            if (id != livro.Id) return NotFound();

            if (ModelState.IsValid)
            {
                _context.Livros.Update(livro);
                _context.SaveChanges();
                return RedirectToAction(nameof(Index));
            }
            return View(livro);
        }

        public IActionResult Delete(int id)
        {
            var livro = _context.Livros.Find(id);
            if (livro == null) return NotFound();
            return View(livro);
        }

        [HttpPost, ActionName("Delete")]
        public IActionResult DeleteConfirmed(int id)
        {
            var livro = _context.Livros.Find(id);
            if (livro != null)
            {
                _context.Livros.Remove(livro);
                _context.SaveChanges();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
