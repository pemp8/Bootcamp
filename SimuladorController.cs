using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using BackEndNETLINUX.Data;
using BackEndNETLINUX.Models;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

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
                string simuladorPath = "C:/Users/mati2/Downloads/Bootcamp-main (3)/Bootcamp-main/BackEndNETLINUX/BackEndNETLINUX/Controllers/simReloj.exe";
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

        [HttpGet("trabajadoresConHorasCero")] // CHECKEAR
        public IActionResult TrabajadoresConHorasCero()
        {
            // Se filtran los registros para obtener los días ausentes
            var registrosCero = _context.RegistrosTiempo.Where(rt =>
                rt.TiempoEntrada == TimeSpan.Zero &&
                rt.TiempoSalida == TimeSpan.Zero &&
                rt.TiempoTrabajado == TimeSpan.Zero
            ).ToList();

            // Se crea la respuesta
            var respuesta = new Dictionary<int, dynamic>();
            foreach (var registro in registrosCero)
            {
                var trabajadorId = registro.TrabajadorID;
                if (!respuesta.ContainsKey(trabajadorId))
                {
                    respuesta[trabajadorId] = new
                    {
                        ID = trabajadorId,
                        DiasCeroHoras = new List<DateTime>()
                    };
                }
                respuesta[trabajadorId].DiasCeroHoras.Add(registro.Fecha);
            }

            var respuestaList = respuesta.Values.ToList();
            return Ok(respuestaList);
        }

        [HttpGet("topTrabajadores")]
        public IActionResult TopTrabajadores([FromQuery] string fecha_inicio, [FromQuery] string fecha_fin)
        {
            // Formateo de url: http://localhost:5000/api/Simulador/topTrabajadores?fecha_inicio=2023-01-01&fecha_fin=2023-01-31
            // Validar que ambos parámetros estén presentes
            if (string.IsNullOrEmpty(fecha_inicio) || string.IsNullOrEmpty(fecha_fin))
            {
                return BadRequest("Debe proporcionar fecha_inicio y fecha_fin");
            }

            DateTime inicio, fin;
            if (!DateTime.TryParse(fecha_inicio, out inicio) || !DateTime.TryParse(fecha_fin, out fin))
            {
                return BadRequest("Formato de fecha no válido");
            }

            // Filtrar registros por el rango de fechas
            var registros = _context.RegistrosTiempo
                .Where(rt => rt.Fecha >= inicio && rt.Fecha <= fin)
                .Include(rt => rt.Trabajador)
                .ToList();

            // Calcular las horas trabajadas
            var trabajadoresHoras = registros
                .GroupBy(rt => rt.TrabajadorID)
                .Select(g => new
                {
                    TrabajadorID = g.Key,
                    TotalMinutos = g.Sum(rt => (rt.TiempoSalida - rt.TiempoEntrada).TotalMinutes)
                })
                .OrderByDescending(th => th.TotalMinutos)
                .Take(5)
                .ToList();

            // Crear la respuesta
            var topTrabajadores = new List<dynamic>();
            int ranking = 1;
            foreach (var th in trabajadoresHoras)
            {
                var trabajador = _context.Trabajadores.FirstOrDefault(t => t.ID == th.TrabajadorID);
                if (trabajador != null)
                {
                    var horas = (int)(th.TotalMinutos / 60);
                    var minutos = (int)(th.TotalMinutos % 60);
                    topTrabajadores.Add(new
                    {
                        trabajador.ID,
                        TotalHoras = $"{horas}:{minutos:00}",
                        Ranking = ranking++
                    });
                }
            }

            var respuesta = new
            {
                periodo_evaluado = new { fecha_inicio, fecha_fin },
                top_trabajadores = topTrabajadores
            };

            return Ok(respuesta);
        }

        [HttpGet("puntualidad")]
        public IActionResult Puntualidad()
        {
            var registros = _context.RegistrosTiempo.Include(rt => rt.Trabajador).ToList();

            // Dictionaries to store punctuality of entry and exit
            var puntualidadEntrada = new Dictionary<int, int>();
            var puntualidadSalida = new Dictionary<int, int>();

            foreach (var registro in registros)
            {
                var trabajadorId = registro.Trabajador.ID;

                // Calculate punctuality of entry
                var horaEntradaReal = registro.TiempoEntrada;
                var horaEntradaEsperada = new TimeSpan(9, 0, 0); // Expected entry time at 9:00
                if (horaEntradaReal > horaEntradaEsperada)
                {
                    if (!puntualidadEntrada.ContainsKey(trabajadorId))
                    {
                        puntualidadEntrada[trabajadorId] = 0;
                    }
                    puntualidadEntrada[trabajadorId]++;
                }

                // Calculate punctuality of exit
                var horaSalidaReal = registro.TiempoSalida;
                var horaSalidaEsperada = new TimeSpan(18, 0, 0); // Expected exit time at 18:00
                if (horaSalidaReal < horaSalidaEsperada)
                {
                    if (!puntualidadSalida.ContainsKey(trabajadorId))
                    {
                        puntualidadSalida[trabajadorId] = 0;
                    }
                    puntualidadSalida[trabajadorId]++;
                }
            }

            // Create the response
            var avisosEntrada = new Dictionary<int, object>();
            var avisosSalida = new Dictionary<int, object>();

            foreach (var item in puntualidadEntrada)
            {
                var trabajadorId = item.Key;
                var contadorEntrada = item.Value;
                avisosEntrada[trabajadorId] = new { contador_entrada_tarde = contadorEntrada };
            }

            foreach (var item in puntualidadSalida)
            {
                var trabajadorId = item.Key;
                var contadorSalida = item.Value;
                avisosSalida[trabajadorId] = new { contador_salida_temprano = contadorSalida };
            }

            var respuesta = new
            {
                avisos_entrada_tarde = avisosEntrada,
                avisos_salida_temprano = avisosSalida
            };

            return Ok(respuesta);
        }

        [HttpGet("horasTrabajadas")]
        public IActionResult HorasTrabajadas([FromQuery] int? trabajadorId, [FromQuery] string fecha)
        {
            try
            {
                // Se verifica que ambos parámetros sean ingresados
                if (!trabajadorId.HasValue || string.IsNullOrEmpty(fecha))
                {
                    return BadRequest("Se requiere el ID del trabajador y la fecha.");
                }

                // Se obtienen el registro del trabajador
                var registro = _context.RegistrosTiempo
                    .FirstOrDefault(rt => rt.TrabajadorID == trabajadorId.Value && rt.Fecha == DateTime.Parse(fecha));

                if (registro == null)
                {
                    return NotFound("No se encontraron registros para el trabajador en la fecha especificada.");
                }

                return Ok(new { horas_trabajadas = registro.TiempoTrabajado });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno del servidor: {ex.Message}");
            }
        }

        [HttpGet("horasNoTrabajadas")]
        public IActionResult HorasNoTrabajadas([FromQuery] int? trabajadorId, [FromQuery] string fecha)
        {
            try
            {
                // Se verifica que ambos parámetros sean ingresados
                if (!trabajadorId.HasValue || string.IsNullOrEmpty(fecha))
                {
                    return BadRequest("Se requiere el ID del trabajador y la fecha.");
                }

                // Se busca el registro del id y día específico
                var registro = _context.RegistrosTiempo
                    .FirstOrDefault(rt => rt.TrabajadorID == trabajadorId.Value && rt.Fecha == DateTime.Parse(fecha));

                if (registro == null)
                {
                    return NotFound("No se encontraron registros para el trabajador en la fecha especificada.");
                }

                // Asumiendo que TiempoTrabajado es un TimeSpan? que representa la duración del trabajo
                if (!registro.TiempoTrabajado.HasValue)
                {
                    return BadRequest("El tiempo trabajado no está disponible para este registro.");
                }

                TimeSpan tiempoTrabajado = registro.TiempoTrabajado.Value;
                int minutosTrabajados = tiempoTrabajado.Hours * 60 + tiempoTrabajado.Minutes;
                int minutosEsperados = 9 * 60; // 9 horas esperadas convertidas a minutos

                // Revisa que tiempoTrabajado sea mayor a 9 horas
                if (minutosTrabajados >= minutosEsperados)
                {
                    // So tiempoTrabajado es mayoro igual a 9 horas, horas_no_trabajadas to "00:00"
                    return Ok(new { horas_no_trabajadas = "00:00" });
                }
                else
                {
                    int minutosNoTrabajados = minutosEsperados - minutosTrabajados;

                    // Convertir minutos no trabajados a horas y minutos
                    int horasNoTrabajadas = minutosNoTrabajados / 60;
                    int minutosMenosHoras = minutosNoTrabajados % 60;

                    // Preparando el tiempo no trabajado para la respuesta
                    TimeSpan tiempoNoTrabajado = new TimeSpan(horasNoTrabajadas, minutosMenosHoras, 0);

                    return Ok(new { horas_no_trabajadas = tiempoNoTrabajado.ToString(@"hh\:mm") });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno del servidor: {ex.Message}");
            }
        }

        [HttpGet("diaMenosHorasTrabajadas")]
        public IActionResult DiaMenosHorasTrabajadas([FromQuery] int? trabajadorId)
        {
            try
            {
                if (!trabajadorId.HasValue)
                {
                    return BadRequest("Se requiere el ID del trabajador.");
                }

                // Fetch the data into memory first
                var registros = _context.RegistrosTiempo
                    .Where(rt => rt.TrabajadorID == trabajadorId.Value)
                    .ToList(); // ToList() fetches the data into memory

                // Then use LINQ to Objects to order and select the record with the least working time
                var registroConMenorTiempo = registros
                    .OrderBy(rt => rt.TiempoTrabajado)
                    .Select(rt => new { rt.Fecha, rt.TiempoTrabajado })
                    .FirstOrDefault();

                if (registroConMenorTiempo == null)
                {
                    return NotFound("No se encontraron registros de tiempo trabajado para el trabajador especificado.");
                }

                // Convertir las horas trabajadas a formato horas:minutos
                var horasTrabajadas = registroConMenorTiempo.TiempoTrabajado.HasValue ? registroConMenorTiempo.TiempoTrabajado.Value.ToString(@"hh\:mm") : "00:00";

                var respuesta = new
                {
                    dia_menor_tiempo = new
                    {
                        fecha = registroConMenorTiempo.Fecha,
                        horas_trabajadas = horasTrabajadas
                    }
                };

                return Ok(respuesta);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno del servidor: {ex.Message}");
            }
        }

        [HttpGet("calcularSueldo")]
        public IActionResult CalcularSueldo([FromQuery] int? trabajadorId, [FromQuery] DateTime? fecha)
        {
            try
            {
                if (!trabajadorId.HasValue || !fecha.HasValue)
                {
                    return BadRequest("Se requiere el ID del trabajador y la fecha.");
                }

                var registro = _context.RegistrosTiempo
                    .FirstOrDefault(rt => rt.TrabajadorID == trabajadorId.Value && rt.Fecha.Date == fecha.Value.Date);

                if (registro == null)
                {
                    return NotFound("No se encontraron registros para el trabajador especificado.");
                }

                TimeSpan tiempoTrabajado = registro.TiempoTrabajado.Value;
                var minutosTrabajados = tiempoTrabajado.Hours * 60 + tiempoTrabajado.Minutes;
                var tiempoTrabajadoTotal = minutosTrabajados / 60;
                var horas = tiempoTrabajado.Hours;

                if (minutosTrabajados == 0)
                {
                    return StatusCode(500, "No se pudo calcular el tiempo trabajado.");
                }

                const decimal sueldoPorHora = 100m;
                var sueldoFinalPorHorasTrabajadas = tiempoTrabajadoTotal * sueldoPorHora;

                var respuesta = new
                {
                    ID = trabajadorId,
                    fecha = fecha.Value.ToString("yyyy-MM-dd"),
                    sueldo = sueldoFinalPorHorasTrabajadas,
                    horasPagadas = horas
                };

                return Ok(respuesta);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno del servidor: {ex.Message}");
            }
        }

        [HttpGet("promedioHorasTrabajadas")]
        public IActionResult PromedioHorasTrabajadas([FromQuery] int? trabajadorId, [FromQuery] string fechaInicio, [FromQuery] string fechaFin)
        {
            try
            {
                if (!trabajadorId.HasValue || string.IsNullOrEmpty(fechaInicio) || string.IsNullOrEmpty(fechaFin))
                {
                    return BadRequest("Se requiere el ID del trabajador, fecha de inicio y fecha de fin.");
                }

                if (!DateTime.TryParse(fechaInicio, out DateTime fechaInicioObj) || !DateTime.TryParse(fechaFin, out DateTime fechaFinObj))
                {
                    return BadRequest("Las fechas proporcionadas no tienen el formato correcto.");
                }

                var registros = _context.RegistrosTiempo
                    .Where(rt => rt.TrabajadorID == trabajadorId.Value && rt.Fecha >= fechaInicioObj && rt.Fecha <= fechaFinObj)
                    .ToList();

                if (!registros.Any())
                {
                    return NotFound("No se encontraron registros de tiempo trabajado para el trabajador en el periodo especificado.");
                }

                int totalTrabajadoEnMins = registros.Sum(registro =>
                {
                    var entrada = registro.TiempoEntrada.Hours * 60 + registro.TiempoEntrada.Minutes;
                    var salida = registro.TiempoSalida.Hours * 60 + registro.TiempoSalida.Minutes;
                    return salida - entrada;
                });

                int numDiasPeriodo = (fechaFinObj - fechaInicioObj).Days + 1;
                int promedioMinutos = totalTrabajadoEnMins / numDiasPeriodo;
                TimeSpan promedio = TimeSpan.FromMinutes(promedioMinutos);

                var respuesta = new
                {
                    trabajadorId = trabajadorId,
                    fechaInicio = fechaInicioObj.ToString("yyyy-MM-dd"),
                    fechaFin = fechaFinObj.ToString("yyyy-MM-dd"),
                    promedioHorasPorDia = $"{promedio.Hours}h {promedio.Minutes}m"
                };

                return Ok(respuesta);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno del servidor: {ex.Message}");
            }
        }

        [HttpGet("horasExtra")] // CHECKEAR
        public IActionResult HorasExtra([FromQuery] int? trabajadorId, [FromQuery] string fechaInicio, [FromQuery] string fechaFin)
        {
            try
            {
                if (!trabajadorId.HasValue || string.IsNullOrEmpty(fechaInicio) || string.IsNullOrEmpty(fechaFin))
                {
                    return BadRequest("Se requiere el ID del trabajador, fecha de inicio y fecha de fin.");
                }

                DateTime fechaInicioObj, fechaFinObj;
                if (!DateTime.TryParse(fechaInicio, out fechaInicioObj) || !DateTime.TryParse(fechaFin, out fechaFinObj))
                {
                    return BadRequest("Formato de fecha no válido");
                }

                var registros = _context.RegistrosTiempo
                    .Where(rt => rt.TrabajadorID == trabajadorId.Value && rt.Fecha >= fechaInicioObj && rt.Fecha <= fechaFinObj)
                    .ToList();

                if (!registros.Any())
                {
                    return NotFound("No se encontraron registros de tiempo trabajado para el trabajador en el periodo especificado.");
                }

                int totalTrabajadoEnMins = 0;
                foreach (var registro in registros)
                {
                    var entrada = registro.TiempoEntrada.Hours * 60 + registro.TiempoEntrada.Minutes;
                    var salida = registro.TiempoSalida.Hours * 60 + registro.TiempoSalida.Minutes;
                    totalTrabajadoEnMins += salida - entrada;
                }

                int numDiasPeriodo = (fechaFinObj - fechaInicioObj).Days + 1;
                int minutosEsperadosPeriodo = numDiasPeriodo * 9 * 60; // 9 hours por dia

                TimeSpan tiempoExtra;
                if (totalTrabajadoEnMins > minutosEsperadosPeriodo)
                {
                    int minutosExtras = totalTrabajadoEnMins - minutosEsperadosPeriodo;
                    tiempoExtra = TimeSpan.FromMinutes(minutosExtras);
                }
                else
                {
                    tiempoExtra = TimeSpan.Zero;
                }

                var respuesta = new
                {
                    ID = trabajadorId,
                    fecha_inicio = fechaInicio,
                    fecha_final = fechaFin,
                    horas_extras = tiempoExtra.ToString(@"hh\:mm")
                };

                return Ok(respuesta);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno del servidor: {ex.Message}");
            }
        }

        [HttpGet("horasDeAlmuerzo")] // CHECKEAR
        public IActionResult HorasDeAlmuerzo()
        {
            try
            {
                // Obtener todos los trabajadores
                var trabajadores = _context.RegistrosTiempo.ToList();

                // Crear un diccionario para almacenar las horas de almuerzo de todos los trabajadores
                var horasAlmuerzo = new Dictionary<int, List<object>>();

                foreach (var trabajador in trabajadores)
                {
                    // Si el tiempo de salida es medianoche, saltar al siguiente ciclo del bucle
                    if (trabajador.TiempoSalida == new TimeSpan(0, 0, 0))
                        continue;

                    // Generar una hora de inicio de almuerzo aleatoria entre las 13:00 y las 14:00
                    var inicioAlmuerzo = new TimeSpan(new Random().Next(13, 15), new Random().Next(0, 60), 0);

                    // Generar una hora de fin de almuerzo aleatoria entre la hora de inicio y las 15:00
                    var finAlmuerzo = new TimeSpan(new Random().Next(inicioAlmuerzo.Hours, 16), new Random().Next(0, 60), 0);

                    // Asegurarse de que la hora de fin de almuerzo es después de la hora de inicio de almuerzo
                    while (finAlmuerzo <= inicioAlmuerzo)
                    {
                        finAlmuerzo = new TimeSpan(new Random().Next(inicioAlmuerzo.Hours, 16), new Random().Next(0, 60), 0);
                    }

                    // Calcular el tiempo total de almuerzo
                    var inicio = DateTime.Today.Add(inicioAlmuerzo);
                    var fin = DateTime.Today.Add(finAlmuerzo);
                    var totalAlmuerzoMin = (fin - inicio).TotalMinutes; // en minutos

                    // Convertir los minutos totales a horas y minutos
                    var totalAlmuerzo = new TimeSpan(0, (int)totalAlmuerzoMin, 0);

                    // Crear un diccionario para este día de trabajo
                    var diaTrabajo = new
                    {
                        Fecha = trabajador.Fecha,
                        InicioDelAlmuerzo = inicioAlmuerzo,
                        FinDelAlmuerzo = finAlmuerzo,
                        TiempoTotalDeAlmuerzo = totalAlmuerzo
                    };

                    // Si este trabajador ya tiene entradas en el diccionario, agregar este día de trabajo a la lista existente
                    if (horasAlmuerzo.ContainsKey(trabajador.TrabajadorID))
                    {
                        horasAlmuerzo[trabajador.TrabajadorID].Add(diaTrabajo);
                    }
                    // Si no, crear una nueva lista para este trabajador
                    else
                    {
                        horasAlmuerzo[trabajador.TrabajadorID] = new List<object> { diaTrabajo };
                    }
                }

                return Ok(horasAlmuerzo);
            }
            catch (Exception e)
            {
                return StatusCode(500, new { error = e.Message });
            }
        }

        [HttpGet("diasTrabajados")]
        public IActionResult GetDiasTrabajados()
        {
            try
            {
                // Obtener todos los trabajadores
                var trabajadores = _context.RegistrosTiempo.ToList();

                // Crear un diccionario para almacenar los días trabajados por cada trabajador
                var diasTrabajados = new Dictionary<int, Dictionary<int, Dictionary<string, HashSet<int>>>>();

                foreach (var trabajador in trabajadores)
                {
                    // Si el tiempo de salida es medianoche, saltar al siguiente ciclo del bucle
                    if (trabajador.TiempoSalida == TimeSpan.Zero)
                        continue;

                    // Extraer el mes y el año de la fecha del trabajador
                    var year = trabajador.Fecha.Year;
                    var month = trabajador.Fecha.Month;

                    // Transformar el número del mes en el nombre del mes
                    var monthName = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(month);

                    // Crear un diccionario para almacenar los días trabajados por cada trabajador en este mes y año
                    if (!diasTrabajados.ContainsKey(trabajador.TrabajadorID))
                        diasTrabajados[trabajador.TrabajadorID] = new Dictionary<int, Dictionary<string, HashSet<int>>>();

                    if (!diasTrabajados[trabajador.TrabajadorID].ContainsKey(year))
                        diasTrabajados[trabajador.TrabajadorID][year] = new Dictionary<string, HashSet<int>>();

                    if (!diasTrabajados[trabajador.TrabajadorID][year].ContainsKey(monthName))
                        diasTrabajados[trabajador.TrabajadorID][year][monthName] = new HashSet<int>();

                    // Agregar la fecha del registro al conjunto de fechas
                    diasTrabajados[trabajador.TrabajadorID][year][monthName].Add(trabajador.Fecha.Day);
                }
                var diasTrabajadosSummary = new Dictionary<int, Dictionary<int, Dictionary<string, string>>>();

                // Convertir los conjuntos de fechas en conteos de días trabajados
                foreach (var trabajadorId in diasTrabajados.Keys)
                {
                    if (!diasTrabajadosSummary.ContainsKey(trabajadorId))
                        diasTrabajadosSummary[trabajadorId] = new Dictionary<int, Dictionary<string, string>>();

                    foreach (var year in diasTrabajados[trabajadorId].Keys)
                    {
                        if (!diasTrabajadosSummary[trabajadorId].ContainsKey(year))
                            diasTrabajadosSummary[trabajadorId][year] = new Dictionary<string, string>();

                        foreach (var monthName in diasTrabajados[trabajadorId][year].Keys)
                        {
                            var days = diasTrabajados[trabajadorId][year][monthName];
                            var diasTrabajadosEnMes = days.Count;
                            var diasNoTrabajados = DateTime.DaysInMonth(year, DateTime.ParseExact(monthName, "MMMM", CultureInfo.CurrentCulture).Month) - diasTrabajadosEnMes;
                            var summary = $"{diasTrabajadosEnMes} días trabajados, {diasNoTrabajados} días no trabajados";

                            diasTrabajadosSummary[trabajadorId][year][monthName] = summary;
                        }
                    }
                }

                return Ok(diasTrabajadosSummary);
            }
            catch (Exception e)
            {
                return StatusCode(500, new { error = e.Message });
            }
        }

        [HttpGet("diasNoTrabajadosConsecutivos")]
        public IActionResult DiasNoTrabajadosConsecutivos()
        {
            var trabajadores = _context.RegistrosTiempo.OrderBy(t => t.Fecha).ToList();

            // Crear un diccionario para almacenar los días no trabajados consecutivos de cada trabajador
            var diasNoTrabajados = trabajadores
                .GroupBy(t => t.TrabajadorID)
                .ToDictionary(g => g.Key, g => new List<DateTime>());

            var diasNoTrabajadosFinal = trabajadores
                .GroupBy(t => t.TrabajadorID)
                .ToDictionary(g => g.Key, g => new List<string>());

            foreach (var trabajador in trabajadores)
            {
                if (trabajador.TiempoSalida == TimeSpan.Zero)
                {
                    diasNoTrabajados[trabajador.TrabajadorID].Add(trabajador.Fecha);
                }
            }

            // Si hay menos de 3 items guardados en la lista, eliminar el trabajador del diccionario
            diasNoTrabajados = diasNoTrabajados
                .Where(d => d.Value.Count >= 3)
                .ToDictionary(d => d.Key, d => d.Value);

            // Chequea si los 3 dias almacenados en la lista son consecutivos
            foreach (var kvp in diasNoTrabajados)
            {
                var trabajadorId = kvp.Key;
                var dias = kvp.Value.OrderBy(d => d).ToList();

                for (int i = 0; i < dias.Count - 2; i++)
                {
                    if ((dias[i + 1] - dias[i]).Days == 1 && (dias[i + 2] - dias[i + 1]).Days == 1)
                    {
                        diasNoTrabajadosFinal[trabajadorId].Add(
                            $"Faltó al trabajo consecutivamente los días: {dias[i]:yyyy-MM-dd}, {dias[i + 1]:yyyy-MM-dd}, {dias[i + 2]:yyyy-MM-dd}"
                        );
                        i += 2; // Saltar los 3 días ya procesados
                    }
                }
            }

            // Si la lista está vacia, eliminar el trabajador del diccionario
            diasNoTrabajadosFinal = diasNoTrabajadosFinal
                .Where(d => d.Value.Any())
                .ToDictionary(d => d.Key, d => d.Value);

            return Ok(diasNoTrabajadosFinal);
        }

        [HttpGet("diaConMasHoras")]
        public IActionResult DiaConMasHoras()
        {
            var trabajadores = _context.RegistrosTiempo.ToList();

            // Crear un diccionario para almacenar los días y las horas trabajadas por cada trabajador
            var horasTrabajadas = new Dictionary<int, List<(TimeSpan, DateTime)>>();

            foreach (var trabajador in trabajadores)
            {
                if (!horasTrabajadas.ContainsKey(trabajador.TrabajadorID))
                {
                    horasTrabajadas[trabajador.TrabajadorID] = new List<(TimeSpan, DateTime)>();
                }
                horasTrabajadas[trabajador.TrabajadorID].Add(((TimeSpan, DateTime))(trabajador.TiempoTrabajado, trabajador.Fecha));
            }

            // Crear un diccionario para almacenar el día con más horas trabajadas por cada trabajador
            var horasMaximasTrabajadas = new Dictionary<int, (TimeSpan, DateTime)>();

            foreach (var trabajadorId in horasTrabajadas.Keys)
            {
                var maxHoras = horasTrabajadas[trabajadorId].MaxBy(h => h.Item1);
                horasMaximasTrabajadas[trabajadorId] = maxHoras;
            }

            // Convertir el resultado a un formato más legible
            var resultado = horasMaximasTrabajadas.ToDictionary(
                k => k.Key,
                v => new { Fecha = v.Value.Item2.ToString("yyyy-MM-dd"), HorasTrabajadas = v.Value.Item1.ToString(@"hh\:mm") }
            );

            return Ok(resultado);
        }

        [HttpGet("ausenciasJustificadas")]
        public IActionResult AusenciasJustificadas()
        {
            var trabajadores = _context.RegistrosTiempo.ToList();
            var ausenciasJustificadas = new Dictionary<int, List<string>>();
            var random = new Random();

            foreach (var trabajador in trabajadores)
            {
                if (!ausenciasJustificadas.ContainsKey(trabajador.TrabajadorID))
                {
                    ausenciasJustificadas[trabajador.TrabajadorID] = new List<string>();
                }

                if (random.Next(1, 4) == 1) // Random.Next is inclusive for the lower bound and exclusive for the upper bound
                {
                    if (trabajador.TiempoSalida == TimeSpan.Zero)
                    {
                        ausenciasJustificadas[trabajador.TrabajadorID].Add($"{trabajador.Fecha:yyyy-MM-dd}: Ausencia justificada");
                    }
                }
            }

            // Eliminar los trabajadores que no tienen ausencias justificadas
            ausenciasJustificadas = ausenciasJustificadas
                .Where(kvp => kvp.Value.Any())
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            return Ok(ausenciasJustificadas);
        }

        [HttpGet("identifTurnosFDS")]
        public IActionResult IdentifTurnosFDS()
        {
            var trabajadores = _context.RegistrosTiempo.ToList();
            var turnosFDS = new Dictionary<int, List<string>>();

            foreach (var trabajador in trabajadores)
            {
                if (!turnosFDS.ContainsKey(trabajador.TrabajadorID))
                {
                    turnosFDS[trabajador.TrabajadorID] = new List<string>();
                }

                // Check if the day of the week is Saturday (6) or Sunday (7)
                if ((int)trabajador.Fecha.DayOfWeek >= 6)
                {
                    if (trabajador.TiempoSalida != TimeSpan.Zero)
                    {
                        turnosFDS[trabajador.TrabajadorID].Add($"Trabajó el fin de semana: {trabajador.Fecha:yyyy-MM-dd}");
                    }
                }
            }

            // Remove workers who did not work on weekends
            turnosFDS = turnosFDS
                .Where(kvp => kvp.Value.Any())
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            return Ok(turnosFDS);
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