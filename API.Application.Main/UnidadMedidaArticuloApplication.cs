using API.Application.DTO;
using API.Application.DTO.articulo.unidad_medida_articulo;
using API.Application.Interface;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace API.Application.Main
{
    public class UnidadMedidaArticuloApplication : IUnidadMedidaArticuloApplication
    {
        private readonly IUnidadMedidaArticuloDomain _unidadMedidaArticuloDomain;
        private readonly IMapper _mapper;

        public UnidadMedidaArticuloApplication(IUnidadMedidaArticuloDomain unidadMedidaArticuloDomain, IMapper mapper)
        {
            _unidadMedidaArticuloDomain = unidadMedidaArticuloDomain;
            _mapper = mapper;
        }

        #region async methods
        public async Task<Respuesta<int>> InsertarAsync(UnidadMedidaArticuloCrearDTO obj)
        {
            var respuesta = new Respuesta<int>();
            try
            {
                var medida = _mapper.Map<UnidadMedidaArticulo>(obj);
                respuesta.Dato = await _unidadMedidaArticuloDomain.InsertarAsync(medida);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Registro agregado correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<bool>> ActualizarAsync(int id, UnidadMedidaArticuloActualizarDTO obj)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                var medida = _mapper.Map<UnidadMedidaArticulo>(obj);
                respuesta.Dato = await _unidadMedidaArticuloDomain.ActualizarAsync(id, medida);
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
                respuesta.Dato = await _unidadMedidaArticuloDomain.EliminarAsync(id);
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

        public async Task<Respuesta<UnidadMedidaArticuloDTO>> ObtenerAsync(int id)
        {
            var respuesta = new Respuesta<UnidadMedidaArticuloDTO>();
            try
            {
                var medida = await _unidadMedidaArticuloDomain.ObtenerAsync(id);
                respuesta.Dato = _mapper.Map<UnidadMedidaArticuloDTO>(medida);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<UnidadMedidaArticuloDTO>> ObtenerAsync(string codigo)
        {
            var respuesta = new Respuesta<UnidadMedidaArticuloDTO>();
            try
            {
                var medida = await _unidadMedidaArticuloDomain.ObtenerAsync(codigo);
                respuesta.Dato = _mapper.Map<UnidadMedidaArticuloDTO>(medida);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<UnidadMedidaArticuloDTO>>> ObtenerContengaNombreAsync(string name)
        {
            var respuesta = new Respuesta<IEnumerable<UnidadMedidaArticuloDTO>>();
            try
            {
                var medidas = await _unidadMedidaArticuloDomain.ObtenerContengaNombreAsync(name);
                respuesta.Dato = _mapper.Map<IEnumerable<UnidadMedidaArticuloDTO>>(medidas);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<UnidadMedidaArticuloDTO>>> ObtenerTodoAsync()
        {
            var respuesta = new Respuesta<IEnumerable<UnidadMedidaArticuloDTO>>();
            try
            {
                var queryable = await _unidadMedidaArticuloDomain.ObtenerTodoAsync();
                var medidas = await queryable.ToListAsync();
                respuesta.Dato = _mapper.Map<IEnumerable<UnidadMedidaArticuloDTO>>(medidas);
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
