using API.Application.DTO;
using API.Application.DTO.moneda;
using API.Application.Interface;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using AutoMapper;

namespace API.Application.Main
{
    public class MonedaApplication : IMonedaApplication
    {
        private readonly IMonedaDomain _monedaDomain;
        private readonly IMapper _mapper;

        public MonedaApplication(IMonedaDomain monedaDomain, IMapper mapper)
        {
            _monedaDomain = monedaDomain;
            _mapper = mapper;
        }

        #region async methods
        public async Task<Respuesta<bool>> InsertarAsync(MonedaCrearDTO obj)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                var moneda = _mapper.Map<Monedum>(obj);
                respuesta.Dato = await _monedaDomain.InsertarAsync(moneda);
                if (respuesta.Dato)
                {
                    respuesta.Resultado = true;
                    respuesta.Mensaje = "Registro agregado correctamente.";
                }
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = $"{ex.Message} / {ex.InnerException}";
            }
            return respuesta;
        }

        public async Task<Respuesta<bool>> ActualizarAsync(string codigo, MonedaActualizarDTO obj)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                var moneda = _mapper.Map<Monedum>(obj);
                respuesta.Dato = await _monedaDomain.ActualizarAsync(codigo, moneda);
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

        public async Task<Respuesta<bool>> EliminarAsync(string codigo)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                respuesta.Dato = await _monedaDomain.EliminarAsync(codigo);
                if (respuesta.Dato)
                {
                    respuesta.Resultado = true;
                    respuesta.Mensaje = "Registro eliminado correctamente.";
                }
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = $"{ex.Message} \n {ex.InnerException}";
            }
            return respuesta;
        }

        public async Task<Respuesta<MonedaDTO>> ObtenerPorCodigoAsync(string codigo)
        {
            var respuesta = new Respuesta<MonedaDTO>();
            try
            {
                var moneda = await _monedaDomain.ObtenerPorCodigoAsync(codigo);
                respuesta.Dato = _mapper.Map<MonedaDTO>(moneda);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Consulta realizada correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<MonedaDTO>> ObtenerPorNombreAsync(string nombre)
        {
            var respuesta = new Respuesta<MonedaDTO>();
            try
            {
                var moneda = await _monedaDomain.ObtenerPorNombreAsync(nombre);
                respuesta.Dato = _mapper.Map<MonedaDTO>(moneda);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Consulta realizada correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<MonedaDTO>>> ObtenerAsync()
        {
            var respuesta = new Respuesta<IEnumerable<MonedaDTO>>();
            try
            {
                var monedas = await _monedaDomain.ObtenerTodoAsync();
                respuesta.Dato = _mapper.Map<IEnumerable<MonedaDTO>>(monedas);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Consulta realizada correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<MonedaDTO>>> ObtenerContengaNombreAsync(string nombre)
        {
            var respuesta = new Respuesta<IEnumerable<MonedaDTO>>();
            try
            {
                var monedas = await _monedaDomain.ObtenerContengaNombreAsync(nombre);
                respuesta.Dato = _mapper.Map<IEnumerable<MonedaDTO>>(monedas);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Consulta realizada correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<MonedaDTO>>> ObtenerContengaCodigoAsync(string codigo)
        {
            var respuesta = new Respuesta<IEnumerable<MonedaDTO>>();
            try
            {
                var monedas = await _monedaDomain.ObtenerContengaCodigoAsync(codigo);
                respuesta.Dato = _mapper.Map<IEnumerable<MonedaDTO>>(monedas);
                if (respuesta.Dato != null)
                {
                    respuesta.Resultado = true;
                    respuesta.Mensaje = "Consulta realizada correctamente.";
                }
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
