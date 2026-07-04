using System.ComponentModel.DataAnnotations;

namespace BibliotecaApp.Models
{
    public class Livro
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "O título é obrigatório")]
        public string Titulo { get; set; } = string.Empty;

        [Required(ErrorMessage = "O autor é obrigatório")]
        public string Autor { get; set; } = string.Empty;

        public string Genero { get; set; } = string.Empty;

        public int AnoPublicacao { get; set; }

        public string Editora { get; set; } = string.Empty;

        public int Quantidade { get; set; }

        public bool Disponivel { get; set; } = true;
    }
}
