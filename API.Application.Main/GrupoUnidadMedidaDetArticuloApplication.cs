using API.Application.DTO;
using API.Application.DTO.articulo.grupo_unidad_medida_det_articulo;
using API.Application.Interface;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace API.Application.Main
{
    public class GrupoUnidadMedidaDetArticuloApplication : IGrupoUnidadMedidaDetArticuloApplication
    {
        private readonly IGrupoUnidadMedidaDetArticuloDomain _grupoUnidadMedidaDetArticuloDomain;
        private readonly IMapper _mapper;

        public GrupoUnidadMedidaDetArticuloApplication(IGrupoUnidadMedidaDetArticuloDomain grupoUnidadMedidaDetArticuloDomain, IMapper mapper)
        {
            _grupoUnidadMedidaDetArticuloDomain = grupoUnidadMedidaDetArticuloDomain;
            _mapper = mapper;
        }

        #region async methods
        public async Task<Respuesta<int>> InsertarAsync(GrupoUnidadMedidaDetArticuloCrearDTO obj)
        {
            var respuesta = new Respuesta<int>();
            try
            {
                var entidad = _mapper.Map<GrupoUnidadMedidaDetArticulo>(obj);
                respuesta.Dato = await _grupoUnidadMedidaDetArticuloDomain.InsertarAsync(entidad);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Registro agregado correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<bool>> ActualizarAsync(int grpMedidaEntry, int numLinea, GrupoUnidadMedidaDetArticuloActualizarDTO obj)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                var entidad = _mapper.Map<GrupoUnidadMedidaDetArticulo>(obj);
                respuesta.Dato = await _grupoUnidadMedidaDetArticuloDomain.ActualizarAsync(grpMedidaEntry, numLinea, entidad);
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

        public async Task<Respuesta<bool>> EliminarAsync(int grpMedidaEntry, int numLinea)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                respuesta.Dato = await _grupoUnidadMedidaDetArticuloDomain.EliminarAsync(grpMedidaEntry, numLinea);
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

        public async Task<Respuesta<GrupoUnidadMedidaDetArticuloDTO>> ObtenerAsync(int grpMedidaEntry, int numLinea)
        {
            var respuesta = new Respuesta<GrupoUnidadMedidaDetArticuloDTO>();
            try
            {
                var entidad = await _grupoUnidadMedidaDetArticuloDomain.ObtenerAsync(grpMedidaEntry, numLinea);
                respuesta.Dato = _mapper.Map<GrupoUnidadMedidaDetArticuloDTO>(entidad);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<GrupoUnidadMedidaDetArticuloDTO>>> ObtenerTodoAsync()
        {
            var respuesta = new Respuesta<IEnumerable<GrupoUnidadMedidaDetArticuloDTO>>();
            try
            {
                var queryable = await _grupoUnidadMedidaDetArticuloDomain.ObtenerTodoAsync();
                var lista = await queryable.ToListAsync();
                respuesta.Dato = _mapper.Map<IEnumerable<GrupoUnidadMedidaDetArticuloDTO>>(lista);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<GrupoUnidadMedidaDetArticuloDTO>>> ObtenerPorGrupoAsync(int grpMedidaEntry)
        {
            var respuesta = new Respuesta<IEnumerable<GrupoUnidadMedidaDetArticuloDTO>>();
            try
            {
                var lista = await _grupoUnidadMedidaDetArticuloDomain.ObtenerPorGrupoAsync(grpMedidaEntry);
                respuesta.Dato = _mapper.Map<IEnumerable<GrupoUnidadMedidaDetArticuloDTO>>(lista);
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
