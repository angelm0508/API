using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;

namespace API.Domain.Core
{
    public class MecanicoDomain : IMecanicoDomain
    {
        private readonly IRepositorioGenerico<Mecanico, string> _repo;

        public MecanicoDomain(IRepositorioGenerico<Mecanico, string> repo)
        {
            _repo = repo;
        }

        #region async methods
        public async Task<bool> InsertarAsync(Mecanico obj)
        {
            obj.Codigo = (obj.Codigo ?? string.Empty).Trim();
            obj.Nombre = (obj.Nombre ?? string.Empty).Trim();
            if (obj.Codigo.Length == 0)
                throw new Exception("El código del mecánico es requerido.");
            if (obj.Nombre.Length == 0)
                throw new Exception("El nombre del mecánico es requerido.");
            obj.Activo = ActivoValido(obj.Activo);

            if (await _repo.ObtenerAsync(obj.Codigo) != null)
                throw new Exception($"Ya existe un registro con el código: {obj.Codigo}");

            await _repo.InsertarAsync(obj);
            return true;
        }

        public async Task<bool> ActualizarAsync(string codigo, Mecanico obj)
        {
            var existente = await _repo.ObtenerAsync(codigo);
            if (existente is null)
                return false;

            obj.Nombre = (obj.Nombre ?? string.Empty).Trim();
            if (obj.Nombre.Length == 0)
                throw new Exception("El nombre del mecánico es requerido.");
            obj.Activo = ActivoValido(obj.Activo);

            return await _repo.ActualizarAsync(codigo, obj);
        }

        public async Task<bool> EliminarAsync(string codigo)
        {
            var existente = await _repo.ObtenerAsync(codigo);
            if (existente is null)
                return false;

            if (existente.Activo == "N")
                return true;

            return await _repo.ActualizarAsync(codigo, new Mecanico
            {
                Nombre = existente.Nombre,
                Telefono = existente.Telefono,
                Activo = "N"
            });
        }

        public async Task<Mecanico?> ObtenerPorCodigoAsync(string codigo)
        {
            return await _repo.ObtenerAsync(codigo);
        }

        public async Task<IQueryable<Mecanico>> ObtenerTodoAsync()
        {
            return await _repo.ObtenerTodoAsync();
        }
        #endregion

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
