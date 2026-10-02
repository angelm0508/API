using API.Application.DTO;
using API.Application.DTO.permiso;
using API.Application.Interface;
using API.Domain.Interface;
using API.Service.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/Plantilla")]
    public class PlantillaController : ControllerBase
    {
        private readonly IPlantillaApplication _plantillaApplication;
        private readonly IPermisoApplication _permisoApplication;

        public PlantillaController(IPlantillaApplication plantillaApplication, IPermisoApplication permisoApplication)
        {
            _plantillaApplication = plantillaApplication;
            _permisoApplication = permisoApplication;
        }

        [HttpGet("{id:int}")]
        [RequierePermiso("Usuarios", Accion.Ver)]
        public async Task<ActionResult<Respuesta<PlantillaDTO>>> Obtener([FromRoute] int id)
        {
            var plantilla = await _plantillaApplication.ObtenerAsync(id);
            if (!plantilla.Resultado) return BadRequest(plantilla);
            if (plantilla.Dato == null)
            {
                plantilla.Resultado = false;
                plantilla.Mensaje = "El código de la plantilla no se encontró.";
                return NotFound(plantilla);
            }
            return Ok(plantilla);
        }

        [HttpGet]
        [RequierePermiso("Usuarios", Accion.Ver)]
        public async Task<ActionResult<Respuesta<IEnumerable<PlantillaDTO>>>> ObtenerTodoAsync()
        {
            var plantillas = await _plantillaApplication.ObtenerTodoAsync();
            return plantillas.Resultado ? Ok(plantillas) : BadRequest(plantillas);
        }

        [HttpPost]
        [RequierePermiso("Usuarios", Accion.Agregar)]
        public async Task<ActionResult<Respuesta<int>>> InsertarAsync([FromBody] PlantillaCrearDTO obj)
        {
            if (User.FindFirst("SuperUsuario")?.Value != "S") return Forbid();
            var insert = await _plantillaApplication.InsertarAsync(obj);
            return insert.Resultado ? Ok(insert) : BadRequest(insert);
        }

        [HttpPut("{id:int}")]
        [RequierePermiso("Usuarios", Accion.Actualizar)]
        public async Task<ActionResult<Respuesta<bool>>> ActualizarAsync([FromRoute] int id, [FromBody] PlantillaActualizarDTO obj)
        {
            if (User.FindFirst("SuperUsuario")?.Value != "S") return Forbid();
            var update = await _plantillaApplication.ActualizarAsync(id, obj);
            return update.Resultado ? Ok(update) : BadRequest(update);
        }

        [HttpDelete("{id:int}")]
        [RequierePermiso("Usuarios", Accion.Eliminar)]
        public async Task<ActionResult<Respuesta<bool>>> EliminarAsync([FromRoute] int id)
        {
            if (User.FindFirst("SuperUsuario")?.Value != "S") return Forbid();
            var delete = await _plantillaApplication.EliminarAsync(id);
            return delete.Resultado ? Ok(delete) : BadRequest(delete);
        }

        [HttpGet("{id:int}/Permisos")]
        [RequierePermiso("Usuarios", Accion.Ver)]
        public async Task<ActionResult<Respuesta<IEnumerable<PermisoModuloDTO>>>> Permisos([FromRoute] int id)
        {
            var res = await _permisoApplication.ObtenerMatrizPlantillaAsync(id);
            return res.Resultado ? Ok(res) : BadRequest(res);
        }

        [HttpPut("{id:int}/Permisos")]
        [RequierePermiso("Usuarios", Accion.Actualizar)]
        public async Task<ActionResult<Respuesta<bool>>> GuardarPermisos([FromRoute] int id, [FromBody] List<PermisoModuloDTO> matriz)
        {
            if (User.FindFirst("SuperUsuario")?.Value != "S") return Forbid();
            var res = await _permisoApplication.GuardarMatrizPlantillaAsync(id, matriz);
            return res.Resultado ? Ok(res) : BadRequest(res);
        }
    }
}
