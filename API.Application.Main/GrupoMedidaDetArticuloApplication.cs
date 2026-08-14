using API.Application.DTO;
using API.Application.DTO.articulo.grupo_medida_det_articulo;
using API.Application.Interface;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace API.Application.Main
{
    public class GrupoMedidaDetArticuloApplication : IGrupoMedidaDetArticuloApplication
    {
        private readonly IGrupoMedidaDetArticuloDomain _grupoMedidaDetArticuloDomain;
        private readonly IMapper _mapper;

        public GrupoMedidaDetArticuloApplication(IGrupoMedidaDetArticuloDomain grupoMedidaDetArticuloDomain, IMapper mapper)
        {
            _grupoMedidaDetArticuloDomain = grupoMedidaDetArticuloDomain;
            _mapper = mapper;
        }

        #region async methods
        public async Task<Respuesta<int>> InsertarAsync(GrupoMedidaDetArticuloCrearDTO obj)
        {
            var respuesta = new Respuesta<int>();
            try
            {
                var grupoMedidaDet = _mapper.Map<GrupoMedidaDetArticulo>(obj);
                respuesta.Dato = await _grupoMedidaDetArticuloDomain.InsertarAsync(grupoMedidaDet);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Registro agregado correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<bool>> ActualizarAsync(int id, GrupoMedidaDetArticuloActualizarDTO obj)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                var grupoMedidaDet = _mapper.Map<GrupoMedidaDetArticulo>(obj);
                respuesta.Dato = await _grupoMedidaDetArticuloDomain.ActualizarAsync(id, grupoMedidaDet);
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
                respuesta.Dato = await _grupoMedidaDetArticuloDomain.EliminarAsync(id);
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

        public async Task<Respuesta<GrupoMedidaDetArticuloDTO>> ObtenerAsync(int id)
        {
            var respuesta = new Respuesta<GrupoMedidaDetArticuloDTO>();
            try
            {
                var grupoMedidaDet = await _grupoMedidaDetArticuloDomain.ObtenerAsync(id);
                respuesta.Dato = _mapper.Map<GrupoMedidaDetArticuloDTO>(grupoMedidaDet);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<GrupoMedidaDetArticuloDTO>>> ObtenerTodoAsync()
        {
            var respuesta = new Respuesta<IEnumerable<GrupoMedidaDetArticuloDTO>>();
            try
            {
                var queryable = await _grupoMedidaDetArticuloDomain.ObtenerTodoAsync();
                var gruposMedidaDet = await queryable.ToListAsync();
                respuesta.Dato = _mapper.Map<IEnumerable<GrupoMedidaDetArticuloDTO>>(gruposMedidaDet);
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
