using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    public class PlantillaDomain : IPlantillaDomain
    {
        private readonly IRepositorioGenerico<Plantilla, int> _repo;
        private readonly IRepositorioGenerico<PlantillaPermisoDetalle, (int PlantillaId, string CodigoModulo)> _repoDetalle;
        private readonly IEjecutorTransaccion _tx;

        public PlantillaDomain(
            IRepositorioGenerico<Plantilla, int> repo,
            IRepositorioGenerico<PlantillaPermisoDetalle, (int PlantillaId, string CodigoModulo)> repoDetalle,
            IEjecutorTransaccion tx)
        {
            _repo = repo;
            _repoDetalle = repoDetalle;
            _tx = tx;
        }

        public async Task<int> InsertarAsync(Plantilla obj)
        {
            var creado = await _repo.InsertarAsync(obj);
            return creado.Id;
        }

        public async Task<bool> ActualizarAsync(int id, Plantilla obj) => await _repo.ActualizarAsync(id, obj);
        // No hay ON DELETE CASCADE en el FK de PlantillaPermisoDetalle: se borran primero las filas de
        // detalle y luego el encabezado, en una sola transaccion. Las filas de UsuarioPermiso copiadas
        // desde la plantilla no se tocan (son copias, sin FK a la plantilla).
        public async Task<bool> EliminarAsync(int id)
        {
            return await _tx.EjecutarAsync(async () =>
            {
                var detalles = await (await _repoDetalle.ObtenerTodoAsync())
                    .Where(d => d.PlantillaId == id)
                    .ToListAsync();
                foreach (var d in detalles)
                    await _repoDetalle.EliminarAsync((d.PlantillaId, d.CodigoModulo));

                return await _repo.EliminarAsync(id);
            });
        }
        public async Task<Plantilla> ObtenerAsync(int id) => await _repo.ObtenerAsync(id);
        public async Task<IQueryable<Plantilla>> ObtenerTodoAsync() => await _repo.ObtenerTodoAsync();
    }
}
