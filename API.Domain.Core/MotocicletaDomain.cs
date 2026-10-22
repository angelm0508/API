using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    public class MotocicletaDomain : IMotocicletaDomain
    {
        private const string TipoSocioCliente = "C";

        private readonly IRepositorioGenerico<Motocicleta, int> _repoMoto;
        private readonly IRepositorioGenerico<SocioNegocio, string> _repoSocio;
        private readonly IRepositorioGenerico<OrdenServicio, int> _repoOrden;

        public MotocicletaDomain(
            IRepositorioGenerico<Motocicleta, int> repoMoto,
            IRepositorioGenerico<SocioNegocio, string> repoSocio,
            IRepositorioGenerico<OrdenServicio, int> repoOrden)
        {
            _repoMoto = repoMoto;
            _repoSocio = repoSocio;
            _repoOrden = repoOrden;
        }

        #region async methods
        public async Task<int> InsertarAsync(Motocicleta obj)
        {
            await NormalizarYValidarAsync(obj, entryActual: null);
            var guardada = await _repoMoto.InsertarAsync(obj);
            return guardada.Entry;
        }

        public async Task<bool> ActualizarAsync(int entry, Motocicleta obj)
        {
            var existente = await _repoMoto.ObtenerAsync(entry);
            if (existente is null)
                return false;

            // Se lee antes de guardar: existente es la entidad rastreada y ActualizarAsync la sobrescribe.
            var clienteAnterior = existente.CodigoSn;

            await NormalizarYValidarAsync(obj, entryActual: entry);

            if (!string.Equals(clienteAnterior, obj.CodigoSn, StringComparison.OrdinalIgnoreCase)
                && await TieneOrdenesAsync(entry))
                throw new Exception("La motocicleta tiene órdenes de servicio y no puede cambiar de cliente.");

            return await _repoMoto.ActualizarAsync(entry, obj);
        }

        public async Task<bool> EliminarAsync(int entry)
        {
            var existente = await _repoMoto.ObtenerAsync(entry);
            if (existente is null)
                return false;

            if (existente.Activo == "N")
                return true;

            return await _repoMoto.ActualizarAsync(entry, new Motocicleta
            {
                CodigoSn = existente.CodigoSn,
                Placa = existente.Placa,
                Marca = existente.Marca,
                Modelo = existente.Modelo,
                Anio = existente.Anio,
                Color = existente.Color,
                Vin = existente.Vin,
                Comentario = existente.Comentario,
                Activo = "N"
            });
        }

        public async Task<Motocicleta?> ObtenerAsync(int entry)
        {
            return await _repoMoto.ObtenerAsync(entry);
        }

        public async Task<IEnumerable<Motocicleta>> BuscarAsync(string? codigoSn, string? placa)
        {
            var consulta = await _repoMoto.ObtenerTodoAsync();

            if (!string.IsNullOrWhiteSpace(codigoSn))
                consulta = consulta.Where(x => x.CodigoSn == codigoSn);

            if (!string.IsNullOrWhiteSpace(placa))
            {
                var fragmento = placa.Trim().ToUpperInvariant();
                consulta = consulta.Where(x => x.Placa.Contains(fragmento));
            }

            return await consulta.OrderBy(x => x.Placa).ToListAsync();
        }

        public async Task<IEnumerable<OrdenServicio>> ObtenerHistorialAsync(int entry)
        {
            var ordenes = await _repoOrden.ObtenerTodoAsync();
            return await ordenes
                .Where(o => o.MotoEntry == entry)
                .OrderByDescending(o => o.FechaDoc).ThenByDescending(o => o.Entry)
                .ToListAsync();
        }
        #endregion

        private async Task NormalizarYValidarAsync(Motocicleta obj, int? entryActual)
        {
            obj.Placa = (obj.Placa ?? string.Empty).Trim().ToUpperInvariant();
            if (obj.Placa.Length == 0)
                throw new Exception("La placa es requerida.");

            obj.CodigoSn = (obj.CodigoSn ?? string.Empty).Trim();
            if (obj.CodigoSn.Length == 0)
                throw new Exception("El cliente de la motocicleta es requerido.");

            var socio = await _repoSocio.ObtenerAsync(obj.CodigoSn)
                ?? throw new Exception("El socio de negocio no existe.");
            if (socio.TipoSn != TipoSocioCliente)
                throw new Exception("El socio de negocio debe ser un cliente.");
            if (socio.Activo == "N")
                throw new Exception("El socio de negocio está inactivo.");

            if (obj.Anio.HasValue && (obj.Anio < 1900 || obj.Anio > 2100))
                throw new Exception("El año de la motocicleta no es válido.");

            if (string.IsNullOrWhiteSpace(obj.Activo))
                obj.Activo = "S";
            else if (obj.Activo != "S" && obj.Activo != "N")
                throw new Exception("El campo Activo debe ser 'S' o 'N'.");

            var motos = await _repoMoto.ObtenerTodoAsync();
            var placa = obj.Placa;
            var excluir = entryActual ?? 0;
            if (await motos.AnyAsync(m => m.Placa == placa && m.Entry != excluir))
                throw new Exception($"Ya existe una motocicleta con la placa: {placa}");
        }

        private async Task<bool> TieneOrdenesAsync(int entry)
        {
            var ordenes = await _repoOrden.ObtenerTodoAsync();
            return await ordenes.AnyAsync(o => o.MotoEntry == entry);
        }
    }
}
