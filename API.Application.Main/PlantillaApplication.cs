using API.Application.DTO;
using API.Application.DTO.permiso;
using API.Application.Interface;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace API.Application.Main
{
    public class PlantillaApplication : IPlantillaApplication
    {
        private readonly IPlantillaDomain _plantillaDomain;
        private readonly IMapper _mapper;

        public PlantillaApplication(IPlantillaDomain plantillaDomain, IMapper mapper)
        {
            _plantillaDomain = plantillaDomain;
            _mapper = mapper;
        }

        public async Task<Respuesta<int>> InsertarAsync(PlantillaCrearDTO obj)
        {
            var respuesta = new Respuesta<int>();
            try
            {
                var plantilla = _mapper.Map<Plantilla>(obj);
                respuesta.Dato = await _plantillaDomain.InsertarAsync(plantilla);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Registro agregado correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<bool>> ActualizarAsync(int id, PlantillaActualizarDTO obj)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                var plantilla = _mapper.Map<Plantilla>(obj);
                respuesta.Dato = await _plantillaDomain.ActualizarAsync(id, plantilla);
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
                respuesta.Dato = await _plantillaDomain.EliminarAsync(id);
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

        public async Task<Respuesta<PlantillaDTO>> ObtenerAsync(int id)
        {
            var respuesta = new Respuesta<PlantillaDTO>();
            try
            {
                var plantilla = await _plantillaDomain.ObtenerAsync(id);
                respuesta.Dato = _mapper.Map<PlantillaDTO>(plantilla);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<PlantillaDTO>>> ObtenerTodoAsync()
        {
            var respuesta = new Respuesta<IEnumerable<PlantillaDTO>>();
            try
            {
                var queryable = await _plantillaDomain.ObtenerTodoAsync();
                var plantillas = await queryable.ToListAsync();
                respuesta.Dato = _mapper.Map<IEnumerable<PlantillaDTO>>(plantillas);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }
    }
}
