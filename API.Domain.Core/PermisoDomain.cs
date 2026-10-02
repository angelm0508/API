using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    public class PermisoDomain : IPermisoDomain
    {
        private readonly IRepositorioGenerico<UsuarioPermiso, (int UsuarioId, string CodigoModulo)> _repoUsuarioPermiso;
        private readonly IRepositorioGenerico<PlantillaPermisoDetalle, (int PlantillaId, string CodigoModulo)> _repoPlantillaDetalle;
        private readonly IEjecutorTransaccion _tx;

        public PermisoDomain(
            IRepositorioGenerico<UsuarioPermiso, (int UsuarioId, string CodigoModulo)> repoUsuarioPermiso,
            IRepositorioGenerico<PlantillaPermisoDetalle, (int PlantillaId, string CodigoModulo)> repoPlantillaDetalle,
            IEjecutorTransaccion tx)
        {
            _repoUsuarioPermiso = repoUsuarioPermiso;
            _repoPlantillaDetalle = repoPlantillaDetalle;
            _tx = tx;
        }

        public async Task<IReadOnlyList<PermisoModulo>> ObtenerMatrizUsuarioAsync(int usuarioId)
        {
            var filas = await (await _repoUsuarioPermiso.ObtenerTodoAsync())
                .Where(p => p.UsuarioId == usuarioId)
                .ToListAsync();
            return filas.Select(p => new PermisoModulo(p.CodigoModulo, p.Ver, p.Agregar, p.Actualizar, p.Eliminar)).ToList();
        }

        public async Task GuardarMatrizUsuarioAsync(int usuarioId, IReadOnlyList<PermisoModulo> matriz)
        {
            await _tx.EjecutarAsync(async () =>
            {
                var existentes = await (await _repoUsuarioPermiso.ObtenerTodoAsync())
                    .Where(p => p.UsuarioId == usuarioId)
                    .ToListAsync();
                foreach (var fila in existentes)
                    await _repoUsuarioPermiso.EliminarAsync((fila.UsuarioId, fila.CodigoModulo));

                foreach (var m in matriz.Where(m => m.Ver || m.Agregar || m.Actualizar || m.Eliminar))
                {
                    await _repoUsuarioPermiso.AgregarSinGuardarAsync(new UsuarioPermiso
                    {
                        UsuarioId = usuarioId,
                        CodigoModulo = m.CodigoModulo,
                        Ver = m.Ver,
                        Agregar = m.Agregar,
                        Actualizar = m.Actualizar,
                        Eliminar = m.Eliminar
                    });
                }
                return true;
            });
        }

        public async Task<IReadOnlyList<PermisoModulo>> ObtenerMatrizPlantillaAsync(int plantillaId)
        {
            var filas = await (await _repoPlantillaDetalle.ObtenerTodoAsync())
                .Where(p => p.PlantillaId == plantillaId)
                .ToListAsync();
            return filas.Select(p => new PermisoModulo(p.CodigoModulo, p.Ver, p.Agregar, p.Actualizar, p.Eliminar)).ToList();
        }

        public async Task GuardarMatrizPlantillaAsync(int plantillaId, IReadOnlyList<PermisoModulo> matriz)
        {
            await _tx.EjecutarAsync(async () =>
            {
                var existentes = await (await _repoPlantillaDetalle.ObtenerTodoAsync())
                    .Where(p => p.PlantillaId == plantillaId)
                    .ToListAsync();
                foreach (var fila in existentes)
                    await _repoPlantillaDetalle.EliminarAsync((fila.PlantillaId, fila.CodigoModulo));

                foreach (var m in matriz.Where(m => m.Ver || m.Agregar || m.Actualizar || m.Eliminar))
                {
                    await _repoPlantillaDetalle.AgregarSinGuardarAsync(new PlantillaPermisoDetalle
                    {
                        PlantillaId = plantillaId,
                        CodigoModulo = m.CodigoModulo,
                        Ver = m.Ver,
                        Agregar = m.Agregar,
                        Actualizar = m.Actualizar,
                        Eliminar = m.Eliminar
                    });
                }
                return true;
            });
        }

        public async Task AplicarPlantillaAsync(int usuarioId, int plantillaId)
        {
            var matrizPlantilla = await ObtenerMatrizPlantillaAsync(plantillaId);
            await GuardarMatrizUsuarioAsync(usuarioId, matrizPlantilla);
        }
    }
}
