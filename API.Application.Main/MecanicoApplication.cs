using API.Application.DTO;
using API.Application.DTO.mecanico;
using API.Application.Interface;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace API.Application.Main
{
    public class MecanicoApplication : IMecanicoApplication
    {
        private readonly IMecanicoDomain _domain;
        private readonly IMapper _mapper;

        public MecanicoApplication(IMecanicoDomain domain, IMapper mapper)
        {
            _domain = domain;
            _mapper = mapper;
        }

        #region async methods
        public async Task<Respuesta<bool>> InsertarAsync(MecanicoCrearDTO obj)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                respuesta.Dato = await _domain.InsertarAsync(_mapper.Map<Mecanico>(obj));
                respuesta.Resultado = respuesta.Dato;
                if (respuesta.Dato)
                    respuesta.Mensaje = "Registro agregado correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<bool>> ActualizarAsync(string codigo, MecanicoActualizarDTO obj)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                respuesta.Dato = await _domain.ActualizarAsync(codigo, _mapper.Map<Mecanico>(obj));
                respuesta.Resultado = respuesta.Dato;
                if (respuesta.Dato)
                    respuesta.Mensaje = "Registro actualizado correctamente.";
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
                respuesta.Dato = await _domain.EliminarAsync(codigo);
                respuesta.Resultado = respuesta.Dato;
                if (respuesta.Dato)
                    respuesta.Mensaje = "Registro desactivado correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<MecanicoDTO>> ObtenerPorCodigoAsync(string codigo)
        {
            var respuesta = new Respuesta<MecanicoDTO>();
            try
            {
                var mecanico = await _domain.ObtenerPorCodigoAsync(codigo);
                respuesta.Dato = _mapper.Map<MecanicoDTO>(mecanico);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Consulta realizada correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<MecanicoDTO>>> ObtenerAsync()
        {
            var respuesta = new Respuesta<IEnumerable<MecanicoDTO>>();
            try
            {
                var mecanicos = await (await _domain.ObtenerTodoAsync()).OrderBy(x => x.Nombre).ToListAsync();
                respuesta.Dato = _mapper.Map<IEnumerable<MecanicoDTO>>(mecanicos);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Consulta realizada correctamente.";
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
