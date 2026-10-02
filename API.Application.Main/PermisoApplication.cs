using API.Application.DTO;
using API.Application.DTO.permiso;
using API.Application.Interface;
using API.Domain.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Application.Main
{
    public class PermisoApplication : IPermisoApplication
    {
        private readonly IPermisoDomain _permisoDomain;
        private readonly IModuloDomain _moduloDomain;

        public PermisoApplication(IPermisoDomain permisoDomain, IModuloDomain moduloDomain)
        {
            _permisoDomain = permisoDomain;
            _moduloDomain = moduloDomain;
        }

        public async Task<Respuesta<IEnumerable<ModuloDTO>>> ObtenerModulosAsync()
        {
            var respuesta = new Respuesta<IEnumerable<ModuloDTO>>();
            try
            {
                var queryable = await _moduloDomain.ObtenerTodoAsync();
                var modulos = await queryable.ToListAsync();
                respuesta.Dato = modulos.Select(m => new ModuloDTO { Codigo = m.Codigo, Nombre = m.Nombre, Grupo = m.Grupo }).ToList();
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<PermisoModuloDTO>>> ObtenerMatrizUsuarioAsync(int usuarioId)
        {
            var respuesta = new Respuesta<IEnumerable<PermisoModuloDTO>>();
            try
            {
                var matriz = await _permisoDomain.ObtenerMatrizUsuarioAsync(usuarioId);
                respuesta.Dato = MapearMatriz(matriz);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<bool>> GuardarMatrizUsuarioAsync(int usuarioId, List<PermisoModuloDTO> matriz)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                await _permisoDomain.GuardarMatrizUsuarioAsync(usuarioId, MapearDTOs(matriz));
                respuesta.Dato = true;
                respuesta.Resultado = true;
                respuesta.Mensaje = "Permisos actualizados correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<PermisoModuloDTO>>> ObtenerMatrizPlantillaAsync(int plantillaId)
        {
            var respuesta = new Respuesta<IEnumerable<PermisoModuloDTO>>();
            try
            {
                var matriz = await _permisoDomain.ObtenerMatrizPlantillaAsync(plantillaId);
                respuesta.Dato = MapearMatriz(matriz);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<bool>> GuardarMatrizPlantillaAsync(int plantillaId, List<PermisoModuloDTO> matriz)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                await _permisoDomain.GuardarMatrizPlantillaAsync(plantillaId, MapearDTOs(matriz));
                respuesta.Dato = true;
                respuesta.Resultado = true;
                respuesta.Mensaje = "Permisos de la plantilla actualizados correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<bool>> AplicarPlantillaAsync(int usuarioId, int plantillaId)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                await _permisoDomain.AplicarPlantillaAsync(usuarioId, plantillaId);
                respuesta.Dato = true;
                respuesta.Resultado = true;
                respuesta.Mensaje = "Plantilla aplicada correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        private static List<PermisoModuloDTO> MapearMatriz(IReadOnlyList<PermisoModulo> matriz) =>
            matriz.Select(m => new PermisoModuloDTO { CodigoModulo = m.CodigoModulo, Ver = m.Ver, Agregar = m.Agregar, Actualizar = m.Actualizar, Eliminar = m.Eliminar }).ToList();

        private static List<PermisoModulo> MapearDTOs(List<PermisoModuloDTO> matriz) =>
            matriz.Select(m => new PermisoModulo(m.CodigoModulo, m.Ver, m.Agregar, m.Actualizar, m.Eliminar)).ToList();
    }
}
