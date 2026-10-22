using API.Application.DTO;
using API.Application.DTO.mecanico;
using API.Application.Interface;
using API.Domain.Interface;
using API.Service.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/Mecanico")]
    public class MecanicoController : ControllerBase
    {
        private readonly IMecanicoApplication _application;

        public MecanicoController(IMecanicoApplication application)
        {
            _application = application;
        }

        [RequierePermiso("Mecanicos", Accion.Ver)]
        [HttpGet]
        public async Task<ActionResult<Respuesta<IEnumerable<MecanicoDTO>>>> ObtenerTodo()
        {
            var mecanicos = await _application.ObtenerAsync();

            if (!mecanicos.Resultado)
                return BadRequest(mecanicos);

            return Ok(mecanicos);
        }

        [RequierePermiso("Mecanicos", Accion.Ver)]
        [HttpGet("{codigo}")]
        public async Task<ActionResult<Respuesta<MecanicoDTO>>> ObtenerPorCodigo([FromRoute] string codigo)
        {
            var mecanico = await _application.ObtenerPorCodigoAsync(codigo);

            if (!mecanico.Resultado)
                return BadRequest(mecanico);

            if (mecanico.Dato == null)
            {
                mecanico.Resultado = false;
                mecanico.Mensaje = "Código de mecánico no encontrado.";
                return NotFound(mecanico);
            }

            return Ok(mecanico);
        }

        [RequierePermiso("Mecanicos", Accion.Agregar)]
        [HttpPost]
        public async Task<ActionResult<Respuesta<bool>>> Crear([FromBody] MecanicoCrearDTO obj)
        {
            var insertar = await _application.InsertarAsync(obj);

            if (!insertar.Resultado)
                return BadRequest(insertar);

            return Ok(insertar);
        }

        [RequierePermiso("Mecanicos", Accion.Actualizar)]
        [HttpPut("{codigo}")]
        public async Task<ActionResult<Respuesta<bool>>> Actualizar([FromRoute] string codigo, [FromBody] MecanicoActualizarDTO obj)
        {
            var mecanico = await _application.ObtenerPorCodigoAsync(codigo);

            if (mecanico.Dato == null)
            {
                mecanico.Resultado = false;
                mecanico.Mensaje = "Código de mecánico no encontrado.";
                return NotFound(mecanico);
            }

            var actualizar = await _application.ActualizarAsync(codigo, obj);

            if (!actualizar.Resultado)
                return BadRequest(actualizar);

            return Ok(actualizar);
        }

        [RequierePermiso("Mecanicos", Accion.Eliminar)]
        [HttpDelete("{codigo}")]
        public async Task<ActionResult<Respuesta<bool>>> Eliminar([FromRoute] string codigo)
        {
            var mecanico = await _application.ObtenerPorCodigoAsync(codigo);

            if (mecanico.Dato == null)
            {
                mecanico.Resultado = false;
                mecanico.Mensaje = "Código de mecánico no encontrado.";
                return NotFound(mecanico);
            }

            var eliminar = await _application.EliminarAsync(codigo);

            if (!eliminar.Resultado)
                return BadRequest(eliminar);

            return Ok(eliminar);
        }
    }
}
