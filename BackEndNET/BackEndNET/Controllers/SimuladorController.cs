using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Text.Json;
using BackEndNET.Data;
using BackEndNET.Models;

namespace BackEndNET.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SimuladorController : ControllerBase
    {
        private readonly AppDbContext _context;

        public SimuladorController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("runsimulador")]
        public IActionResult RunSimulador()
        {
            try
            {
                string simuladorPath = "./trabajadores/bin/simReloj";
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = simuladorPath,
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                    }
                };

                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    return StatusCode(500, "Error al ejecutar el simulador");
                }

                var trabajadoresData = JsonSerializer.Deserialize<List<RegistroTiempoDTO>>(output);

                foreach (var trabajadorData in trabajadoresData)
                {
                    var trabajador = _context.Trabajadores
                        .FirstOrDefault(t => t.ID == trabajadorData.ID) ?? new Trabajador { ID = trabajadorData.ID };

                    if (trabajador.ID == 0)
                    {
                        _context.Trabajadores.Add(trabajador);
                    }

                    var registro = new RegistroTiempo
                    {
                        TrabajadorID = trabajador.ID,
                        Fecha = DateTime.Parse(trabajadorData.Fecha),
                        TiempoEntrada = TimeSpan.Parse(trabajadorData.TiempoEntrada),
                        TiempoSalida = TimeSpan.Parse(trabajadorData.TiempoSalida)
                    };
                    registro.CalcularTiempoTrabajado();
                    _context.RegistrosTiempo.Add(registro);
                }

                _context.SaveChanges();
                return Ok("Simulador ejecutado y datos almacenados");
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpGet("runmasdias")]
        public IActionResult RunMasDias([FromQuery] int dias)
        {
            try
            {
                if (dias <= 0)
                {
                    return BadRequest("Número de días debe ser mayor que cero.");
                }

                var trabajadores = _context.Trabajadores.ToList();

                if (!trabajadores.Any())
                {
                    return NotFound("No hay trabajadores en la base de datos.");
                }

                var random = new Random();
                foreach (var trabajador in trabajadores)
                {
                    var registros = _context.RegistrosTiempo
                        .Where(rt => rt.TrabajadorID == trabajador.ID)
                        .OrderByDescending(rt => rt.Fecha)
                        .ToList();

                    var fechaMaxima = registros.Any() ? registros.First().Fecha : DateTime.Now;

                    for (var i = 1; i <= dias; i++)
                    {
                        var nuevaFecha = fechaMaxima.AddDays(i);

                        if (!_context.RegistrosTiempo.Any(rt => rt.TrabajadorID == trabajador.ID && rt.Fecha == nuevaFecha))
                        {
                            var tiempoEntrada = new TimeSpan(random.Next(8, 12), random.Next(0, 60), 0);
                            var tiempoSalida = new TimeSpan(random.Next(16, 20), random.Next(0, 60), 0);

                            var registro = new RegistroTiempo
                            {
                                TrabajadorID = trabajador.ID,
                                Fecha = nuevaFecha,
                                TiempoEntrada = tiempoEntrada,
                                TiempoSalida = tiempoSalida
                            };
                            registro.CalcularTiempoTrabajado();
                            _context.RegistrosTiempo.Add(registro);
                        }
                    }
                }

                _context.SaveChanges();
                return Ok($"Datos generados para {dias} días para todas las IDs existentes.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }
    }

    // DTO para deserializar los datos JSON
    public class RegistroTiempoDTO
    {
        public int ID { get; set; }
        public string Fecha { get; set; }
        public string TiempoEntrada { get; set; }
        public string TiempoSalida { get; set; }
    }
}