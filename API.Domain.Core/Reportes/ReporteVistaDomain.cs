using System.Text.Json;
using API.Domain.Entity.Models;
using API.Domain.Interface.Reportes;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core.Reportes
{
    /// <summary>Vistas guardadas de reportes: configuración JSON por usuario y reporte, con nombre único (sin distinguir mayúsculas).</summary>
    public sealed class ReporteVistaDomain : IReporteVistaDomain
    {
        public const int MaxVistasPorUsuario = 50;
        public const int MaxLongitudNombre = 100;
        public const int MaxTamanoConfiguracion = 20000;

        private readonly IRepositorioGenerico<ReporteVista, int> _repo;

        public ReporteVistaDomain(IRepositorioGenerico<ReporteVista, int> repo)
        {
            _repo = repo;
        }

        public async Task<IReadOnlyList<ReporteVistaDato>> ObtenerAsync(int usuarioId, string codigoReporte)
        {
            var q = await _repo.ObtenerTodoAsync();
            var vistas = await q.Where(v => v.UsuarioId == usuarioId && v.CodigoReporte == codigoReporte)
                                .OrderBy(v => v.Nombre)
                                .ToListAsync();
            return vistas.Select(Dato).ToList();
        }

        public async Task<ReporteVistaDato> GuardarAsync(int usuarioId, string codigoReporte, string nombre, string configuracionJson)
        {
            nombre = (nombre ?? "").Trim();
            if (nombre.Length == 0)
                throw new ReporteInvalidoException("El nombre de la vista es obligatorio.");
            if (nombre.Length > MaxLongitudNombre)
                throw new ReporteInvalidoException($"El nombre de la vista no puede superar {MaxLongitudNombre} caracteres.");
            ValidarConfiguracion(configuracionJson);

            var q = await _repo.ObtenerTodoAsync();
            var minuscula = nombre.ToLower();
            var existente = await q.Where(v => v.UsuarioId == usuarioId && v.CodigoReporte == codigoReporte && v.Nombre.ToLower() == minuscula)
                                   .FirstOrDefaultAsync();
            var ahora = DateTime.Now;

            if (existente is not null)
            {
                existente.Nombre = nombre;
                existente.Configuracion = configuracionJson;
                existente.FechaActualizacion = ahora;
                await _repo.ActualizarAsync(existente.Entry, existente);
                return Dato(existente);
            }

            var total = await q.CountAsync(v => v.UsuarioId == usuarioId);
            if (total >= MaxVistasPorUsuario)
                throw new ReporteInvalidoException($"Ya tienes {MaxVistasPorUsuario} vistas guardadas. Borra alguna para crear otra.");

            var creada = await _repo.InsertarAsync(new ReporteVista
            {
                UsuarioId = usuarioId,
                CodigoReporte = codigoReporte,
                Nombre = nombre,
                Configuracion = configuracionJson,
                FechaCreacion = ahora,
                FechaActualizacion = ahora
            });
            return Dato(creada);
        }

        public async Task<bool> EliminarAsync(int usuarioId, string codigoReporte, int entry)
        {
            var q = await _repo.ObtenerTodoAsync();
            var vista = await q.Where(v => v.Entry == entry && v.UsuarioId == usuarioId && v.CodigoReporte == codigoReporte)
                               .FirstOrDefaultAsync();
            if (vista is null) return false;
            return await _repo.EliminarAsync(entry);
        }

        private static void ValidarConfiguracion(string? json)
        {
            if (json is not null && json.Length > MaxTamanoConfiguracion)
                throw new ReporteInvalidoException("La configuración de la vista es demasiado grande.");
            if (string.IsNullOrWhiteSpace(json))
                throw new ReporteInvalidoException("La configuración de la vista no es válida.");

            try
            {
                using var documento = JsonDocument.Parse(json);
                if (documento.RootElement.ValueKind != JsonValueKind.Object)
                    throw new ReporteInvalidoException("La configuración de la vista no es válida.");
            }
            catch (JsonException)
            {
                throw new ReporteInvalidoException("La configuración de la vista no es válida.");
            }
        }

        private static ReporteVistaDato Dato(ReporteVista v) => new(v.Entry, v.Nombre, v.Configuracion, v.FechaActualizacion);
    }
}
