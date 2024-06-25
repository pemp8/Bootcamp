using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using BackEndNETLINUX.Data;

var builder = WebApplication.CreateBuilder(args);

// Configurar el contexto de la base de datos para SQLite
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// A�adir servicios necesarios, incluyendo autorizaci�n
builder.Services.AddControllers();
builder.Services.AddAuthorization(); // A�adir servicio de autorizaci�n

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthorization(); // Middleware de autorizaci�n

app.MapControllers();

app.Run();