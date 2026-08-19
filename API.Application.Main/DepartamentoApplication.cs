using API.Application.DTO;
using API.Application.DTO.departamento;
using API.Application.Interface;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using AutoMapper;

namespace API.Application.Main
{
    public class DepartamentoApplication : IDepartamentoApplication
    {
        private readonly IDepartamentoDomain _departamentoDomain;
        private readonly IMapper _mapper;

        public DepartamentoApplication(IDepartamentoDomain departamentoDomain, IMapper mapper)
        {
            _departamentoDomain = departamentoDomain;
            _mapper = mapper;
        }

        #region async methods
        public async Task<Respuesta<bool>> InsertarAsync(DepartamentoCrearDTO obj)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                var departamento = _mapper.Map<Departamento>(obj);
                respuesta.Dato = await _departamentoDomain.InsertarAsync(departamento);
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

        public async Task<Respuesta<bool>> ActualizarAsync(string codigo, DepartamentoActualizarDTO obj)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                var departamento = _mapper.Map<Departamento>(obj);
                respuesta.Dato = await _departamentoDomain.ActualizarAsync(codigo, departamento);
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
                respuesta.Dato = await _departamentoDomain.EliminarAsync(codigo);
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

        public async Task<Respuesta<DepartamentoDTO>> ObtenerPorCodigoAsync(string codigo)
        {
            var respuesta = new Respuesta<DepartamentoDTO>();
            try
            {
                var departamento = await _departamentoDomain.ObtenerPorCodigoAsync(codigo);
                respuesta.Dato = _mapper.Map<DepartamentoDTO>(departamento);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Consulta realizada correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<DepartamentoDTO>> ObtenerPorNombreAsync(string nombre)
        {
            var respuesta = new Respuesta<DepartamentoDTO>();
            try
            {
                var departamento = await _departamentoDomain.ObtenerPorNombreAsync(nombre);
                respuesta.Dato = _mapper.Map<DepartamentoDTO>(departamento);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Consulta realizada correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<DepartamentoDTO>>> ObtenerAsync()
        {
            var respuesta = new Respuesta<IEnumerable<DepartamentoDTO>>();
            try
            {
                var departamentos = await _departamentoDomain.ObtenerTodoAsync();
                respuesta.Dato = _mapper.Map<IEnumerable<DepartamentoDTO>>(departamentos);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Consulta realizada correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<DepartamentoDTO>>> ObtenerContengaNombreAsync(string nombre)
        {
            var respuesta = new Respuesta<IEnumerable<DepartamentoDTO>>();
            try
            {
                var departamentos = await _departamentoDomain.ObtenerContengaNombreAsync(nombre);
                respuesta.Dato = _mapper.Map<IEnumerable<DepartamentoDTO>>(departamentos);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Consulta realizada correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<DepartamentoDTO>>> ObtenerContengaCodigoAsync(string codigo)
        {
            var respuesta = new Respuesta<IEnumerable<DepartamentoDTO>>();
            try
            {
                var departamentos = await _departamentoDomain.ObtenerContengaCodigoAsync(codigo);
                respuesta.Dato = _mapper.Map<IEnumerable<DepartamentoDTO>>(departamentos);
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
