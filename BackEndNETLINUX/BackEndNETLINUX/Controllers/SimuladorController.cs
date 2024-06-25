using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using BackEndNETLINUX.Data;
using BackEndNETLINUX.Models;
using Microsoft.EntityFrameworkCore;

namespace BackEndNETLINUX.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
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
                string simuladorPath = "C:/Users/carlo/source/repos/BackEndNETLINUX/BackEndNETLINUX/Controllers/simReloj.exe";
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

                if (string.IsNullOrEmpty(output))
                {
                    return StatusCode(500, "Error: La salida del simulador está vacía.");
                }

                List<RegistroTiempoDTO> trabajadoresData;
                try
                {
                    trabajadoresData = JsonSerializer.Deserialize<List<RegistroTiempoDTO>>(output);
                }
                catch (JsonException ex)
                {
                    return StatusCode(500, $"Error al deserializar los datos del simulador: {ex.Message}");
                }

                if (trabajadoresData == null)
                {
                    return StatusCode(500, "Error: Datos del simulador no válidos.");
                }

                foreach (var trabajadorData in trabajadoresData)
                {
                    // Verificar si el trabajador ya existe en la base de datos
                    var trabajador = _context.Trabajadores.FirstOrDefault(t => t.ID == trabajadorData.ID);

                    if (trabajador == null)
                    {
                        // Si el trabajador no existe, crearlo
                        trabajador = new Trabajador(); // No establecer ID aquí
                        _context.Trabajadores.Add(trabajador);
                        _context.SaveChanges(); // Guardar cambios para obtener el ID generado
                    }

                    // Crear el registro de tiempo
                    var registro = new RegistroTiempo
                    {
                        TrabajadorID = trabajador.ID,
                        Fecha = DateTime.Parse(trabajadorData.fecha),
                        TiempoEntrada = TimeSpan.Parse(trabajadorData.tiempo_entrada),
                        TiempoSalida = TimeSpan.Parse(trabajadorData.tiempo_salida)
                    };
                    registro.CalcularTiempoTrabajado();
                    _context.RegistrosTiempo.Add(registro);
                }

                _context.SaveChanges(); // Guardar todos los registros de tiempo
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
                    var registrosExistentes = _context.RegistrosTiempo
                        .Where(rt => rt.TrabajadorID == trabajador.ID)
                        .ToList();

                    var fechaMaxima = registrosExistentes.Any() ? registrosExistentes.Max(rt => rt.Fecha) : DateTime.Now.Date;

                    for (var i = 1; i <= dias; i++)
                    {
                        var nuevaFecha = fechaMaxima.AddDays(i);

                        if (!_context.RegistrosTiempo.Any(rt => rt.TrabajadorID == trabajador.ID && rt.Fecha.Date == nuevaFecha.Date))
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
                return Ok($"Datos generados para {dias} días para todos los trabajadores existentes.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        // Método GET para obtener todos los registros de tiempo
        [HttpGet("registrosTiempo")]
        public IActionResult GetRegistrosTiempo()
        {
            try
            {
                var registros = _context.RegistrosTiempo
                    .Include(rt => rt.Trabajador) // Incluir datos relacionados si es necesario
                    .ToList();

                return Ok(registros);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error al obtener los registros de tiempo: {ex.Message}");
            }
        }

        [HttpGet("clean")]
        public IActionResult CleanDatabase()
        {
            try
            {
                // Eliminar todos los registros en la tabla RegistrosTiempo
                var registrosTiempo = _context.RegistrosTiempo.ToList();
                _context.RegistrosTiempo.RemoveRange(registrosTiempo);

                // Eliminar todos los registros en la tabla Trabajadores
                var trabajadores = _context.Trabajadores.ToList();
                _context.Trabajadores.RemoveRange(trabajadores);

                // Guardar cambios
                _context.SaveChanges();


                return Ok("Base de datos limpiada con éxito.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error al limpiar la base de datos: {ex.Message}");
            }
        }
    }

    // DTO para deserializar los datos JSON
    public class RegistroTiempoDTO
    {
        public int ID { get; set; }
        public string fecha { get; set; }
        public string tiempo_entrada { get; set; }
        public string tiempo_salida { get; set; }
        public string tiempo_trabajado { get; set; }
    }
}