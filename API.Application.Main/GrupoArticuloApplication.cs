using API.Application.DTO;
using API.Application.DTO.articulo.grupo_articulo;
using API.Application.Interface;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using AutoMapper;
using Azure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace API.Application.Main
{
    public class GrupoArticuloApplication : IGrupoArticuloApplication
    {
        private readonly IGrupoArticuloDomain _grupoArticuloDomain;
        private readonly IMapper _mapper;

        public GrupoArticuloApplication(IGrupoArticuloDomain grupoArticuloDomain, IMapper mapper)
        {
            _grupoArticuloDomain = grupoArticuloDomain;
            _mapper = mapper;
        }

        #region async methods
        public async Task<Respuesta<int>> InsertarAsync(GrupoArticuloCrearDTO obj)
        {
            var respuesta = new Respuesta<int>();
            try
            {
                var brand = _mapper.Map<GrupoArticulo>(obj);
                respuesta.Dato = await _grupoArticuloDomain.InsertarAsync(brand);


                respuesta.Resultado = true;
                respuesta.Mensaje = "Registro agregado correctamente.";

            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }
        public async Task<Respuesta<bool>> ActualizarAsync(int id, GrupoArticuloActualizarDTO obj)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                var brand = _mapper.Map<GrupoArticulo>(obj);
                respuesta.Dato = await _grupoArticuloDomain.ActualizarAsync(id, brand);
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
                respuesta.Dato = await _grupoArticuloDomain.EliminarAsync(id);
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
        public async Task<Respuesta<GrupoArticuloDTO>> ObtenerAsync(int id)
        {
            var respuesta = new Respuesta<GrupoArticuloDTO>();
            try
            {
                var brand = await _grupoArticuloDomain.ObtenerAsync(id);
                respuesta.Dato = _mapper.Map<GrupoArticuloDTO>(brand);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Consulta realizada correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }
        public async Task<Respuesta<GrupoArticuloDTO>> ObtenerAsync(string name)
        {
            var respuesta = new Respuesta<GrupoArticuloDTO>();
            try
            {
                var product = await _grupoArticuloDomain.ObtenerAsync(name);
                respuesta.Dato = _mapper.Map<GrupoArticuloDTO>(product);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Consulta realizada correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }
        public async Task<Respuesta<IEnumerable<GrupoArticuloDTO>>> ObtenerTodoAsync()
        {
            var respuesta = new Respuesta<IEnumerable<GrupoArticuloDTO>>();
            try
            {
                var brands = await _grupoArticuloDomain.ObtenerTodoAsync();
                respuesta.Dato = _mapper.Map<IEnumerable<GrupoArticuloDTO>>(brands);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Consulta realizada correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<GrupoArticuloDTO>>> ObtenerContengaNombreAsync(string name)
        {
            var respuesta = new Respuesta<IEnumerable<GrupoArticuloDTO>>();
            try
            {
                var brands = await _grupoArticuloDomain.ObtenerContengaNombreAsync(name);
                respuesta.Dato = _mapper.Map<IEnumerable<GrupoArticuloDTO>>(brands);
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
