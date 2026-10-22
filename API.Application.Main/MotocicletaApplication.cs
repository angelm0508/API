using API.Application.DTO;
using API.Application.DTO.motocicleta;
using API.Application.Interface;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using AutoMapper;

namespace API.Application.Main
{
    public class MotocicletaApplication : IMotocicletaApplication
    {
        private readonly IMotocicletaDomain _domain;
        private readonly IMapper _mapper;

        public MotocicletaApplication(IMotocicletaDomain domain, IMapper mapper)
        {
            _domain = domain;
            _mapper = mapper;
        }

        #region async methods
        public async Task<Respuesta<int>> InsertarAsync(MotocicletaCrearDTO obj)
        {
            var respuesta = new Respuesta<int>();
            try
            {
                respuesta.Dato = await _domain.InsertarAsync(_mapper.Map<Motocicleta>(obj));
                respuesta.Resultado = true;
                respuesta.Mensaje = "Registro agregado correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<bool>> ActualizarAsync(int entry, MotocicletaActualizarDTO obj)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                respuesta.Dato = await _domain.ActualizarAsync(entry, _mapper.Map<Motocicleta>(obj));
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

        public async Task<Respuesta<bool>> EliminarAsync(int entry)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                respuesta.Dato = await _domain.EliminarAsync(entry);
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

        public async Task<Respuesta<MotocicletaDTO>> ObtenerAsync(int entry)
        {
            var respuesta = new Respuesta<MotocicletaDTO>();
            try
            {
                respuesta.Dato = _mapper.Map<MotocicletaDTO>(await _domain.ObtenerAsync(entry));
                respuesta.Resultado = true;
                respuesta.Mensaje = "Consulta realizada correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<MotocicletaDTO>>> BuscarAsync(string? codigoSn, string? placa)
        {
            var respuesta = new Respuesta<IEnumerable<MotocicletaDTO>>();
            try
            {
                respuesta.Dato = _mapper.Map<IEnumerable<MotocicletaDTO>>(await _domain.BuscarAsync(codigoSn, placa));
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
