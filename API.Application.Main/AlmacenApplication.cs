using API.Application.DTO;
using API.Application.DTO.almacen;
using API.Application.Interface;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using AutoMapper;

namespace API.Application.Main
{
    public class AlmacenApplication : IAlmacenApplication
    {
        private readonly IAlmacenDomain _almacenDomain;
        private readonly IMapper _mapper;

        public AlmacenApplication(IAlmacenDomain almacenDomain, IMapper mapper)
        {
            _almacenDomain = almacenDomain;
            _mapper = mapper;
        }

        #region async methods
        public async Task<Respuesta<bool>> InsertarAsync(AlmacenCrearDTO obj)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                var almacen = _mapper.Map<Almacen>(obj);
                respuesta.Dato = await _almacenDomain.InsertarAsync(almacen);
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

        public async Task<Respuesta<bool>> ActualizarAsync(string codigo, AlmacenActualizarDTO obj)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                var almacen = _mapper.Map<Almacen>(obj);
                respuesta.Dato = await _almacenDomain.ActualizarAsync(codigo, almacen);
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
                respuesta.Dato = await _almacenDomain.EliminarAsync(codigo);
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

        public async Task<Respuesta<AlmacenDTO>> ObtenerPorCodigoAsync(string codigo)
        {
            var respuesta = new Respuesta<AlmacenDTO>();
            try
            {
                var almacen = await _almacenDomain.ObtenerPorCodigoAsync(codigo);
                respuesta.Dato = _mapper.Map<AlmacenDTO>(almacen);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Consulta realizada correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<AlmacenDTO>> ObtenerPorNombreAsync(string nombre)
        {
            var respuesta = new Respuesta<AlmacenDTO>();
            try
            {
                var almacen = await _almacenDomain.ObtenerPorNombreAsync(nombre);
                respuesta.Dato = _mapper.Map<AlmacenDTO>(almacen);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Consulta realizada correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<AlmacenDTO>>> ObtenerAsync()
        {
            var respuesta = new Respuesta<IEnumerable<AlmacenDTO>>();
            try
            {
                var almacenes = await _almacenDomain.ObtenerTodoAsync();
                respuesta.Dato = _mapper.Map<IEnumerable<AlmacenDTO>>(almacenes);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Consulta realizada correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<AlmacenDTO>>> ObtenerContengaNombreAsync(string nombre)
        {
            var respuesta = new Respuesta<IEnumerable<AlmacenDTO>>();
            try
            {
                var almacenes = await _almacenDomain.ObtenerContengaNombreAsync(nombre);
                respuesta.Dato = _mapper.Map<IEnumerable<AlmacenDTO>>(almacenes);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Consulta realizada correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<AlmacenDTO>>> ObtenerContengaCodigoAsync(string codigo)
        {
            var respuesta = new Respuesta<IEnumerable<AlmacenDTO>>();
            try
            {
                var almacenes = await _almacenDomain.ObtenerContengaCodigoAsync(codigo);
                respuesta.Dato = _mapper.Map<IEnumerable<AlmacenDTO>>(almacenes);
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
