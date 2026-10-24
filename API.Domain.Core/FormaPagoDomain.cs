using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    public class FormaPagoDomain : IFormaPagoDomain
    {
        private readonly IRepositorioGenerico<FormaPago, string> _repo;
        private readonly IRepositorioGenerico<PagoRecibidoMedio, (int Entry, int NoLinea)> _repoMedio;

        public FormaPagoDomain(
            IRepositorioGenerico<FormaPago, string> repo,
            IRepositorioGenerico<PagoRecibidoMedio, (int Entry, int NoLinea)> repoMedio)
        {
            _repo = repo;
            _repoMedio = repoMedio;
        }

        #region async methods
        public async Task<bool> InsertarAsync(FormaPago obj)
        {
            obj.Codigo = (obj.Codigo ?? string.Empty).Trim();
            obj.Nombre = (obj.Nombre ?? string.Empty).Trim();
            if (obj.Codigo.Length == 0)
                throw new Exception("El código de la forma de pago es requerido.");
            if (obj.Nombre.Length == 0)
                throw new Exception("El nombre de la forma de pago es requerido.");
            obj.TipoDato = TipoDatoValido(obj.TipoDato);
            obj.Activo = ActivoValido(obj.Activo);

            if (await _repo.ObtenerAsync(obj.Codigo) != null)
                throw new Exception($"Ya existe un registro con el código: {obj.Codigo}");

            await _repo.InsertarAsync(obj);
            return true;
        }

        public async Task<bool> ActualizarAsync(string codigo, FormaPago obj)
        {
            var existente = await _repo.ObtenerAsync(codigo);
            if (existente is null)
                return false;

            obj.Nombre = (obj.Nombre ?? string.Empty).Trim();
            if (obj.Nombre.Length == 0)
                throw new Exception("El nombre de la forma de pago es requerido.");
            obj.TipoDato = string.IsNullOrWhiteSpace(obj.TipoDato) ? existente.TipoDato : TipoDatoValido(obj.TipoDato);
            obj.Activo = ActivoValido(obj.Activo);

            // El tipo de dato fija qué referencia exige cada pago ya registrado: no puede cambiar si la forma se usó.
            if (obj.TipoDato != existente.TipoDato && await TieneMediosAsync(codigo))
                throw new Exception("No se puede cambiar el tipo de dato: la forma de pago ya se usó en pagos.");

            return await _repo.ActualizarAsync(codigo, obj);
        }

        public async Task<bool> EliminarAsync(string codigo)
        {
            var existente = await _repo.ObtenerAsync(codigo);
            if (existente is null)
                return false;

            if (existente.Activo == "N")
                return true;

            return await _repo.ActualizarAsync(codigo, new FormaPago
            {
                Nombre = existente.Nombre,
                TipoDato = existente.TipoDato,
                Activo = "N"
            });
        }

        public async Task<FormaPago?> ObtenerPorCodigoAsync(string codigo)
        {
            return await _repo.ObtenerAsync(codigo);
        }

        public async Task<IQueryable<FormaPago>> ObtenerTodoAsync()
        {
            return await _repo.ObtenerTodoAsync();
        }
        #endregion

        private async Task<bool> TieneMediosAsync(string codigo)
        {
            var medios = await _repoMedio.ObtenerTodoAsync();
            return await medios.AnyAsync(m => m.CodFormaPago == codigo);
        }

        private static string TipoDatoValido(string? tipo)
        {
            if (string.IsNullOrWhiteSpace(tipo))
                return "N";
            if (tipo != "N" && tipo != "C" && tipo != "T" && tipo != "J")
                throw new Exception("El tipo de dato debe ser 'N', 'C', 'T' o 'J'.");
            return tipo;
        }

        private static string ActivoValido(string? activo)
        {
            if (string.IsNullOrWhiteSpace(activo))
                return "S";
            if (activo != "S" && activo != "N")
                throw new Exception("El campo Activo debe ser 'S' o 'N'.");
            return activo;
        }
    }
}
