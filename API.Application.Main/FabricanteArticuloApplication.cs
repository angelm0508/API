using API.Application.DTO;
using API.Application.DTO.articulo.fabricante_articulo;
using API.Application.Interface;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace API.Application.Main
{
    public class FabricanteArticuloApplication : IFabricanteArticuloApplication
    {
        private readonly IFabricanteArticuloDomain _fabricanteArticuloDomain;
        private readonly IMapper _mapper;

        public FabricanteArticuloApplication(IFabricanteArticuloDomain fabricanteArticuloDomain, IMapper mapper)
        {
            _fabricanteArticuloDomain = fabricanteArticuloDomain;
            _mapper = mapper;
        }

        #region async methods
        public async Task<Respuesta<int>> InsertarAsync(FabricanteArticuloCrearDTO obj)
        {
            var respuesta = new Respuesta<int>();
            try
            {
                var fabricante = _mapper.Map<FabricanteArticulo>(obj);
                respuesta.Dato = await _fabricanteArticuloDomain.InsertarAsync(fabricante);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Registro agregado correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<bool>> ActualizarAsync(int id, FabricanteArticuloActualizarDTO obj)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                var fabricante = _mapper.Map<FabricanteArticulo>(obj);
                respuesta.Dato = await _fabricanteArticuloDomain.ActualizarAsync(id, fabricante);
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
                respuesta.Dato = await _fabricanteArticuloDomain.EliminarAsync(id);
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

        public async Task<Respuesta<FabricanteArticuloDTO>> ObtenerAsync(int id)
        {
            var respuesta = new Respuesta<FabricanteArticuloDTO>();
            try
            {
                var fabricante = await _fabricanteArticuloDomain.ObtenerAsync(id);
                respuesta.Dato = _mapper.Map<FabricanteArticuloDTO>(fabricante);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<FabricanteArticuloDTO>> ObtenerAsync(string name)
        {
            var respuesta = new Respuesta<FabricanteArticuloDTO>();
            try
            {
                var fabricante = await _fabricanteArticuloDomain.ObtenerAsync(name);
                respuesta.Dato = _mapper.Map<FabricanteArticuloDTO>(fabricante);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<FabricanteArticuloDTO>>> ObtenerContengaNombreAsync(string name)
        {
            var respuesta = new Respuesta<IEnumerable<FabricanteArticuloDTO>>();
            try
            {
                var fabricantes = await _fabricanteArticuloDomain.ObtenerContengaNombreAsync(name);
                respuesta.Dato = _mapper.Map<IEnumerable<FabricanteArticuloDTO>>(fabricantes);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<FabricanteArticuloDTO>>> ObtenerTodoAsync()
        {
            var respuesta = new Respuesta<IEnumerable<FabricanteArticuloDTO>>();
            try
            {
                var queryable = await _fabricanteArticuloDomain.ObtenerTodoAsync();
                var fabricantes = await queryable.ToListAsync();
                respuesta.Dato = _mapper.Map<IEnumerable<FabricanteArticuloDTO>>(fabricantes);
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
