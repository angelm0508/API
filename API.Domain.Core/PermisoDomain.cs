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
        private readonly IRepositorioGenerico<Plantilla, int> _repoPlantilla;
        private readonly IRepositorioGenerico<Modulo, string> _repoModulo;
        private readonly IEjecutorTransaccion _tx;

        public PermisoDomain(
            IRepositorioGenerico<UsuarioPermiso, (int UsuarioId, string CodigoModulo)> repoUsuarioPermiso,
            IRepositorioGenerico<PlantillaPermisoDetalle, (int PlantillaId, string CodigoModulo)> repoPlantillaDetalle,
            IRepositorioGenerico<Plantilla, int> repoPlantilla,
            IRepositorioGenerico<Modulo, string> repoModulo,
            IEjecutorTransaccion tx)
        {
            _repoUsuarioPermiso = repoUsuarioPermiso;
            _repoPlantillaDetalle = repoPlantillaDetalle;
            _repoPlantilla = repoPlantilla;
            _repoModulo = repoModulo;
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

                // Se conservan tambien las filas todo-false (la matriz queda explicita). Sin filas = sin acceso:
                // un usuario sin permisos asignados no ve ni puede hacer nada (salvo SuperUsuario).
                foreach (var m in matriz)
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
            var plantilla = await _repoPlantilla.ObtenerAsync(plantillaId);
            if (plantilla is null)
                throw new Exception("La plantilla no existe.");

            var matrizPlantilla = await ObtenerMatrizPlantillaAsync(plantillaId);
            var porModulo = matrizPlantilla.ToDictionary(m => m.CodigoModulo);

            // Copia completa: una fila por cada modulo del catalogo (flags de la plantilla si los
            // tiene, todo-false si no), para que el usuario quede con matriz propia y
            // explicita para todo el catalogo.
            var modulos = await (await _repoModulo.ObtenerTodoAsync()).ToListAsync();
            var matrizUsuario = modulos
                .Select(mod => porModulo.TryGetValue(mod.Codigo, out var fila)
                    ? fila
                    : new PermisoModulo(mod.Codigo, false, false, false, false))
                .ToList();

            await GuardarMatrizUsuarioAsync(usuarioId, matrizUsuario);
        }
    }
}