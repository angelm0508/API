using API.Application.DTO;
using API.Application.DTO.usuario.usuario;
using API.Application.Interface;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace API.Application.Main
{
    public class UsuarioApplication : IUsuarioApplication
    {
        private readonly IUsuarioDomain _usuarioDomain;
        private readonly IMapper _mapper;

        public UsuarioApplication(IUsuarioDomain usuarioDomain, IMapper mapper)
        {
            _usuarioDomain = usuarioDomain;
            _mapper = mapper;
        }

        #region async methods
        public async Task<Respuesta<int>> InsertarAsync(UsuarioCrearDTO obj)
        {
            var respuesta = new Respuesta<int>();
            try
            {
                var usuario = _mapper.Map<Usuario>(obj);
                respuesta.Dato = await _usuarioDomain.InsertarAsync(usuario);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Registro agregado correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<bool>> ActualizarAsync(int id, UsuarioActualizarDTO obj)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                var usuario = _mapper.Map<Usuario>(obj);
                respuesta.Dato = await _usuarioDomain.ActualizarAsync(id, usuario);
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
                respuesta.Dato = await _usuarioDomain.EliminarAsync(id);
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

        public async Task<Respuesta<UsuarioDTO>> ObtenerAsync(int id)
        {
            var respuesta = new Respuesta<UsuarioDTO>();
            try
            {
                var usuario = await _usuarioDomain.ObtenerAsync(id);
                respuesta.Dato = _mapper.Map<UsuarioDTO>(usuario);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<UsuarioDTO>> ObtenerAsync(string codigo)
        {
            var respuesta = new Respuesta<UsuarioDTO>();
            try
            {
                var usuario = await _usuarioDomain.ObtenerAsync(codigo);
                respuesta.Dato = _mapper.Map<UsuarioDTO>(usuario);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<UsuarioDTO>>> ObtenerContengaNombreAsync(string name)
        {
            var respuesta = new Respuesta<IEnumerable<UsuarioDTO>>();
            try
            {
                var usuarios = await _usuarioDomain.ObtenerContengaNombreAsync(name);
                respuesta.Dato = _mapper.Map<IEnumerable<UsuarioDTO>>(usuarios);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<UsuarioDTO>>> ObtenerTodoAsync()
        {
            var respuesta = new Respuesta<IEnumerable<UsuarioDTO>>();
            try
            {
                var queryable = await _usuarioDomain.ObtenerTodoAsync();
                var usuarios = await queryable.ToListAsync();
                respuesta.Dato = _mapper.Map<IEnumerable<UsuarioDTO>>(usuarios);
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
