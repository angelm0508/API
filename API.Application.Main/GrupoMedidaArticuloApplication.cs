using API.Application.DTO;
using API.Application.DTO.articulo.grupo_medida_articulo;
using API.Application.Interface;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace API.Application.Main
{
    public class GrupoMedidaArticuloApplication : IGrupoMedidaArticuloApplication
    {
        private readonly IGrupoMedidaArticuloDomain _grupoMedidaArticuloDomain;
        private readonly IMapper _mapper;

        public GrupoMedidaArticuloApplication(IGrupoMedidaArticuloDomain grupoMedidaArticuloDomain, IMapper mapper)
        {
            _grupoMedidaArticuloDomain = grupoMedidaArticuloDomain;
            _mapper = mapper;
        }

        #region async methods
        public async Task<Respuesta<int>> InsertarAsync(GrupoMedidaArticuloCrearDTO obj)
        {
            var respuesta = new Respuesta<int>();
            try
            {
                var grupoMedida = _mapper.Map<GrupoMedidaArticulo>(obj);
                respuesta.Dato = await _grupoMedidaArticuloDomain.InsertarAsync(grupoMedida);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Registro agregado correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<bool>> ActualizarAsync(int id, GrupoMedidaArticuloActualizarDTO obj)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                var grupoMedida = _mapper.Map<GrupoMedidaArticulo>(obj);
                respuesta.Dato = await _grupoMedidaArticuloDomain.ActualizarAsync(id, grupoMedida);
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
                respuesta.Dato = await _grupoMedidaArticuloDomain.EliminarAsync(id);
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

        public async Task<Respuesta<GrupoMedidaArticuloDTO>> ObtenerAsync(int id)
        {
            var respuesta = new Respuesta<GrupoMedidaArticuloDTO>();
            try
            {
                var grupoMedida = await _grupoMedidaArticuloDomain.ObtenerAsync(id);
                respuesta.Dato = _mapper.Map<GrupoMedidaArticuloDTO>(grupoMedida);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<GrupoMedidaArticuloDTO>> ObtenerAsync(string name)
        {
            var respuesta = new Respuesta<GrupoMedidaArticuloDTO>();
            try
            {
                var grupoMedida = await _grupoMedidaArticuloDomain.ObtenerAsync(name);
                respuesta.Dato = _mapper.Map<GrupoMedidaArticuloDTO>(grupoMedida);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<GrupoMedidaArticuloDTO>>> ObtenerContengaNombreAsync(string name)
        {
            var respuesta = new Respuesta<IEnumerable<GrupoMedidaArticuloDTO>>();
            try
            {
                var gruposMedida = await _grupoMedidaArticuloDomain.ObtenerContengaNombreAsync(name);
                respuesta.Dato = _mapper.Map<IEnumerable<GrupoMedidaArticuloDTO>>(gruposMedida);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<GrupoMedidaArticuloDTO>>> ObtenerTodoAsync()
        {
            var respuesta = new Respuesta<IEnumerable<GrupoMedidaArticuloDTO>>();
            try
            {
                var queryable = await _grupoMedidaArticuloDomain.ObtenerTodoAsync();
                var gruposMedida = await queryable.ToListAsync();
                respuesta.Dato = _mapper.Map<IEnumerable<GrupoMedidaArticuloDTO>>(gruposMedida);
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
