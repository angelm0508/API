using API.Application.DTO;
using API.Application.DTO.articulo.medida_articulo;
using API.Application.Interface;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace API.Application.Main
{
    public class MedidaArticuloApplication : IMedidaArticuloApplication
    {
        private readonly IMedidaArticuloDomain _medidaArticuloDomain;
        private readonly IMapper _mapper;

        public MedidaArticuloApplication(IMedidaArticuloDomain medidaArticuloDomain, IMapper mapper)
        {
            _medidaArticuloDomain = medidaArticuloDomain;
            _mapper = mapper;
        }

        #region async methods
        public async Task<Respuesta<int>> InsertarAsync(MedidaArticuloCrearDTO obj)
        {
            var respuesta = new Respuesta<int>();
            try
            {
                var medida = _mapper.Map<MedidaArticulo>(obj);
                respuesta.Dato = await _medidaArticuloDomain.InsertarAsync(medida);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Registro agregado correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<bool>> ActualizarAsync(int id, MedidaArticuloActualizarDTO obj)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                var medida = _mapper.Map<MedidaArticulo>(obj);
                respuesta.Dato = await _medidaArticuloDomain.ActualizarAsync(id, medida);
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
                respuesta.Dato = await _medidaArticuloDomain.EliminarAsync(id);
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

        public async Task<Respuesta<MedidaArticuloDTO>> ObtenerAsync(int id)
        {
            var respuesta = new Respuesta<MedidaArticuloDTO>();
            try
            {
                var medida = await _medidaArticuloDomain.ObtenerAsync(id);
                respuesta.Dato = _mapper.Map<MedidaArticuloDTO>(medida);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<MedidaArticuloDTO>> ObtenerAsync(string codigo)
        {
            var respuesta = new Respuesta<MedidaArticuloDTO>();
            try
            {
                var medida = await _medidaArticuloDomain.ObtenerAsync(codigo);
                respuesta.Dato = _mapper.Map<MedidaArticuloDTO>(medida);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<MedidaArticuloDTO>>> ObtenerContengaNombreAsync(string name)
        {
            var respuesta = new Respuesta<IEnumerable<MedidaArticuloDTO>>();
            try
            {
                var medidas = await _medidaArticuloDomain.ObtenerContengaNombreAsync(name);
                respuesta.Dato = _mapper.Map<IEnumerable<MedidaArticuloDTO>>(medidas);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<MedidaArticuloDTO>>> ObtenerTodoAsync()
        {
            var respuesta = new Respuesta<IEnumerable<MedidaArticuloDTO>>();
            try
            {
                var queryable = await _medidaArticuloDomain.ObtenerTodoAsync();
                var medidas = await queryable.ToListAsync();
                respuesta.Dato = _mapper.Map<IEnumerable<MedidaArticuloDTO>>(medidas);
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
