using API.Application.DTO;
using API.Application.DTO.precio.listado_precio;
using API.Application.Interface;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace API.Application.Main
{
    public class ListadoPrecioApplication : IListadoPrecioApplication
    {
        private readonly IListadoPrecioDomain _listadoPrecioDomain;
        private readonly IMapper _mapper;

        public ListadoPrecioApplication(IListadoPrecioDomain listadoPrecioDomain, IMapper mapper)
        {
            _listadoPrecioDomain = listadoPrecioDomain;
            _mapper = mapper;
        }

        #region async methods
        public async Task<Respuesta<int>> InsertarAsync(ListadoPrecioCrearDTO obj)
        {
            var respuesta = new Respuesta<int>();
            try
            {
                var listadoPrecio = _mapper.Map<ListadoPrecio>(obj);
                respuesta.Dato = await _listadoPrecioDomain.InsertarAsync(listadoPrecio);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Registro agregado correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<bool>> ActualizarAsync(int id, ListadoPrecioActualizarDTO obj)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                var listadoPrecio = _mapper.Map<ListadoPrecio>(obj);
                respuesta.Dato = await _listadoPrecioDomain.ActualizarAsync(id, listadoPrecio);
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
                respuesta.Dato = await _listadoPrecioDomain.EliminarAsync(id);
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

        public async Task<Respuesta<ListadoPrecioDTO>> ObtenerAsync(int id)
        {
            var respuesta = new Respuesta<ListadoPrecioDTO>();
            try
            {
                var listadoPrecio = await _listadoPrecioDomain.ObtenerAsync(id);
                respuesta.Dato = _mapper.Map<ListadoPrecioDTO>(listadoPrecio);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<ListadoPrecioDTO>> ObtenerAsync(string name)
        {
            var respuesta = new Respuesta<ListadoPrecioDTO>();
            try
            {
                var listadoPrecio = await _listadoPrecioDomain.ObtenerAsync(name);
                respuesta.Dato = _mapper.Map<ListadoPrecioDTO>(listadoPrecio);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<ListadoPrecioDTO>>> ObtenerContengaNombreAsync(string name)
        {
            var respuesta = new Respuesta<IEnumerable<ListadoPrecioDTO>>();
            try
            {
                var listadoPrecios = await _listadoPrecioDomain.ObtenerContengaNombreAsync(name);
                respuesta.Dato = _mapper.Map<IEnumerable<ListadoPrecioDTO>>(listadoPrecios);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<ListadoPrecioDTO>>> ObtenerTodoAsync()
        {
            var respuesta = new Respuesta<IEnumerable<ListadoPrecioDTO>>();
            try
            {
                var queryable = await _listadoPrecioDomain.ObtenerTodoAsync();
                var listadoPrecios = await queryable.ToListAsync();
                respuesta.Dato = _mapper.Map<IEnumerable<ListadoPrecioDTO>>(listadoPrecios);
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
