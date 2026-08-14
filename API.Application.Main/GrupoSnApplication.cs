using API.Application.DTO;
using API.Application.DTO.articulo.grupo_sn;
using API.Application.Interface;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace API.Application.Main
{
    public class GrupoSnApplication : IGrupoSnApplication
    {
        private readonly IGrupoSnDomain _grupoSnDomain;
        private readonly IMapper _mapper;

        public GrupoSnApplication(IGrupoSnDomain grupoSnDomain, IMapper mapper)
        {
            _grupoSnDomain = grupoSnDomain;
            _mapper = mapper;
        }

        #region async methods
        public async Task<Respuesta<int>> InsertarAsync(GrupoSnCrearDTO obj)
        {
            var respuesta = new Respuesta<int>();
            try
            {
                var grupoSn = _mapper.Map<GrupoSn>(obj);
                respuesta.Dato = await _grupoSnDomain.InsertarAsync(grupoSn);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Registro agregado correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<bool>> ActualizarAsync(int id, GrupoSnActualizarDTO obj)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                var grupoSn = _mapper.Map<GrupoSn>(obj);
                respuesta.Dato = await _grupoSnDomain.ActualizarAsync(id, grupoSn);
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
                respuesta.Dato = await _grupoSnDomain.EliminarAsync(id);
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

        public async Task<Respuesta<GrupoSnDTO>> ObtenerAsync(int id)
        {
            var respuesta = new Respuesta<GrupoSnDTO>();
            try
            {
                var grupoSn = await _grupoSnDomain.ObtenerAsync(id);
                respuesta.Dato = _mapper.Map<GrupoSnDTO>(grupoSn);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<GrupoSnDTO>> ObtenerAsync(string name)
        {
            var respuesta = new Respuesta<GrupoSnDTO>();
            try
            {
                var grupoSn = await _grupoSnDomain.ObtenerAsync(name);
                respuesta.Dato = _mapper.Map<GrupoSnDTO>(grupoSn);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<GrupoSnDTO>>> ObtenerContengaNombreAsync(string name)
        {
            var respuesta = new Respuesta<IEnumerable<GrupoSnDTO>>();
            try
            {
                var gruposSn = await _grupoSnDomain.ObtenerContengaNombreAsync(name);
                respuesta.Dato = _mapper.Map<IEnumerable<GrupoSnDTO>>(gruposSn);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<GrupoSnDTO>>> ObtenerTodoAsync()
        {
            var respuesta = new Respuesta<IEnumerable<GrupoSnDTO>>();
            try
            {
                var queryable = await _grupoSnDomain.ObtenerTodoAsync();
                var gruposSn = await queryable.ToListAsync();
                respuesta.Dato = _mapper.Map<IEnumerable<GrupoSnDTO>>(gruposSn);
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
