using API.Application.DTO;
using API.Application.DTO.articulo.grupo_unidad_medida_articulo;
using API.Application.Interface;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace API.Application.Main
{
    public class GrupoUnidadMedidaArticuloApplication : IGrupoUnidadMedidaArticuloApplication
    {
        private readonly IGrupoUnidadMedidaArticuloDomain _grupoUnidadMedidaArticuloDomain;
        private readonly IMapper _mapper;

        public GrupoUnidadMedidaArticuloApplication(IGrupoUnidadMedidaArticuloDomain grupoUnidadMedidaArticuloDomain, IMapper mapper)
        {
            _grupoUnidadMedidaArticuloDomain = grupoUnidadMedidaArticuloDomain;
            _mapper = mapper;
        }

        #region async methods
        public async Task<Respuesta<int>> InsertarAsync(GrupoUnidadMedidaArticuloCrearDTO obj)
        {
            var respuesta = new Respuesta<int>();
            try
            {
                var grupoMedida = _mapper.Map<GrupoUnidadMedidaArticulo>(obj);
                respuesta.Dato = await _grupoUnidadMedidaArticuloDomain.InsertarAsync(grupoMedida);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Registro agregado correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<bool>> ActualizarAsync(int id, GrupoUnidadMedidaArticuloActualizarDTO obj)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                var grupoMedida = _mapper.Map<GrupoUnidadMedidaArticulo>(obj);
                respuesta.Dato = await _grupoUnidadMedidaArticuloDomain.ActualizarAsync(id, grupoMedida);
                if (respuesta.Dato)
                {
                    respuesta.Resultado = true;
                    respuesta.Mensaje = "Registro actualizado correctamente.";
                }
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<bool>> EliminarAsync(int id)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                respuesta.Dato = await _grupoUnidadMedidaArticuloDomain.EliminarAsync(id);
                if (respuesta.Dato)
                {
                    respuesta.Resultado = true;
                    respuesta.Mensaje = "Registro eliminado correctamente.";
                }
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<GrupoUnidadMedidaArticuloDTO>> ObtenerAsync(int id)
        {
            var respuesta = new Respuesta<GrupoUnidadMedidaArticuloDTO>();
            try
            {
                var grupoMedida = await _grupoUnidadMedidaArticuloDomain.ObtenerAsync(id);
                respuesta.Dato = _mapper.Map<GrupoUnidadMedidaArticuloDTO>(grupoMedida);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<GrupoUnidadMedidaArticuloDTO>> ObtenerAsync(string name)
        {
            var respuesta = new Respuesta<GrupoUnidadMedidaArticuloDTO>();
            try
            {
                var grupoMedida = await _grupoUnidadMedidaArticuloDomain.ObtenerAsync(name);
                respuesta.Dato = _mapper.Map<GrupoUnidadMedidaArticuloDTO>(grupoMedida);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<GrupoUnidadMedidaArticuloDTO>>> ObtenerContengaNombreAsync(string name)
        {
            var respuesta = new Respuesta<IEnumerable<GrupoUnidadMedidaArticuloDTO>>();
            try
            {
                var gruposMedida = await _grupoUnidadMedidaArticuloDomain.ObtenerContengaNombreAsync(name);
                respuesta.Dato = _mapper.Map<IEnumerable<GrupoUnidadMedidaArticuloDTO>>(gruposMedida);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<GrupoUnidadMedidaArticuloDTO>>> ObtenerTodoAsync()
        {
            var respuesta = new Respuesta<IEnumerable<GrupoUnidadMedidaArticuloDTO>>();
            try
            {
                var queryable = await _grupoUnidadMedidaArticuloDomain.ObtenerTodoAsync();
                var gruposMedida = await queryable.ToListAsync();
                respuesta.Dato = _mapper.Map<IEnumerable<GrupoUnidadMedidaArticuloDTO>>(gruposMedida);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }
        #endregion
    }
}
