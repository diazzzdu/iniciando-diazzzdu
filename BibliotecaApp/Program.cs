using BibliotecaApp.Data;
using Microsoft.EntityFrameworkCore;

builder.Services.AddDbContext<BibliotecaContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("BibliotecaContext")));