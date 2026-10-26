using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;

namespace API.Domain.Core
{
    public class CondicionPagoDomain : ICondicionPagoDomain
    {
        /// <summary>Condición de respaldo del sistema (Contado): no se puede dar de baja.</summary>
        private const string CodigoContado = "CON";

        private readonly IRepositorioGenerico<CondicionPago, string> _repo;

        public CondicionPagoDomain(IRepositorioGenerico<CondicionPago, string> repo)
        {
            _repo = repo;
        }

        #region async methods
        public async Task<bool> InsertarAsync(CondicionPago obj)
        {
            obj.Codigo = (obj.Codigo ?? string.Empty).Trim();
            obj.Nombre = (obj.Nombre ?? string.Empty).Trim();
            if (obj.Codigo.Length == 0)
                throw new Exception("El código de la condición de pago es requerido.");
            if (obj.Nombre.Length == 0)
                throw new Exception("El nombre de la condición de pago es requerido.");
            ValidarDias(obj.Dias);
            obj.Activo = ActivoValido(obj.Activo);

            if (await _repo.ObtenerAsync(obj.Codigo) != null)
                throw new Exception($"Ya existe un registro con el código: {obj.Codigo}");

            await _repo.InsertarAsync(obj);
            return true;
        }

        public async Task<bool> ActualizarAsync(string codigo, CondicionPago obj)
        {
            var existente = await _repo.ObtenerAsync(codigo);
            if (existente is null)
                return false;

            obj.Nombre = (obj.Nombre ?? string.Empty).Trim();
            if (obj.Nombre.Length == 0)
                throw new Exception("El nombre de la condición de pago es requerido.");
            ValidarDias(obj.Dias);
            obj.Activo = ActivoValido(obj.Activo);

            if (obj.Activo == "N" && EsContado(codigo))
                throw new Exception(MensajeContadoNoSeDaDeBaja);
            if (obj.Dias != 0 && EsContado(codigo))
                throw new Exception("La condición CON debe tener 0 días.");

            return await _repo.ActualizarAsync(codigo, obj);
        }

        public async Task<bool> EliminarAsync(string codigo)
        {
            if (EsContado(codigo))
                throw new Exception(MensajeContadoNoSeDaDeBaja);

            var existente = await _repo.ObtenerAsync(codigo);
            if (existente is null)
                return false;

            if (existente.Activo == "N")
                return true;

            return await _repo.ActualizarAsync(codigo, new CondicionPago
            {
                Nombre = existente.Nombre,
                Dias = existente.Dias,
                Activo = "N"
            });
        }

        public async Task<CondicionPago?> ObtenerPorCodigoAsync(string codigo)
        {
            return await _repo.ObtenerAsync(codigo);
        }

        public async Task<IQueryable<CondicionPago>> ObtenerTodoAsync()
        {
            return await _repo.ObtenerTodoAsync();
        }

        public async Task<CondicionPago?> ObtenerActivaAsync(string codigo)
        {
            var condicion = await _repo.ObtenerAsync(codigo);
            if (condicion is null)
                throw new Exception($"La condición de pago {codigo} no existe.");
            if (condicion.Activo == "N")
                throw new Exception($"La condición de pago {codigo} está inactiva.");
            return condicion;
        }
        #endregion

        private const string MensajeContadoNoSeDaDeBaja = "La condición CON es la predeterminada del sistema y no se puede dar de baja.";

        private static bool EsContado(string? codigo) =>
            string.Equals((codigo ?? string.Empty).Trim(), CodigoContado, StringComparison.OrdinalIgnoreCase);

        private static void ValidarDias(int dias)
        {
            if (dias < 0 || dias > 365)
                throw new Exception("Los días de crédito deben estar entre 0 y 365.");
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
