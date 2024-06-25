using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using BackEndNETLINUX.Data;

var builder = WebApplication.CreateBuilder(args);

// Configurar el contexto de la base de datos para SQLite
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Añadir servicios necesarios, incluyendo autorización
builder.Services.AddControllers();
builder.Services.AddAuthorization(); // Añadir servicio de autorización

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthorization(); // Middleware de autorización

app.MapControllers();

app.Run();