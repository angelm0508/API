using API.Application.DTO;
using API.Application.DTO.direccionSocioNegocio;
using API.Application.Interface;
using API.Domain.Interface;
using API.Service.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/DireccionSocioNegocio")]
    public class DireccionSocioNegocioController : ControllerBase
    {
        private readonly IDireccionSocioNegocioApplication _direccionApplication;

        public DireccionSocioNegocioController(IDireccionSocioNegocioApplication direccionApplication)
        {
            _direccionApplication = direccionApplication;
        }

        [RequierePermiso("SociosNegocio", Accion.Ver)]
        [HttpGet("{codigo}")]
        public async Task<ActionResult<Respuesta<DireccionSocioNegocioDTO>>> ObtenerPorCodigo([FromRoute] string codigo)
        {
            var direccion = await _direccionApplication.ObtenerPorCodigoAsync(codigo);

            if (!direccion.Resultado)
                return BadRequest(direccion);

            if (direccion.Dato == null)
            {
                direccion.Resultado = false;
                direccion.Mensaje = "Código de dirección no encontrado.";
                return NotFound(direccion);
            }

            return Ok(direccion);
        }

        [RequierePermiso("SociosNegocio", Accion.Ver)]
        [HttpGet("ContengaCodigo/{codigo}")]
        public async Task<ActionResult<Respuesta<IEnumerable<DireccionSocioNegocioDTO>>>> ObtenerContengaCodigo([FromRoute] string codigo)
        {
            var direcciones = await _direccionApplication.ObtenerContengaCodigoAsync(codigo);

            if (!direcciones.Resultado)
                return BadRequest(direcciones);

            return Ok(direcciones);
        }

        [RequierePermiso("SociosNegocio", Accion.Ver)]
        [HttpGet()]
        public async Task<ActionResult<Respuesta<IEnumerable<DireccionSocioNegocioDTO>>>> ObtenerTodo()
        {
            var direcciones = await _direccionApplication.ObtenerAsync();

            if (!direcciones.Resultado)
                return BadRequest(direcciones);

            return Ok(direcciones);
        }

        [RequierePermiso("SociosNegocio", Accion.Agregar)]
        [HttpPost]
        public async Task<ActionResult<Respuesta<bool>>> Crear([FromBody] DireccionSocioNegocioCrearDTO obj)
        {
            var insertar = await _direccionApplication.InsertarAsync(obj);

            if (!insertar.Resultado)
                return BadRequest(insertar);

            return Ok(insertar);
        }

        [RequierePermiso("SociosNegocio", Accion.Actualizar)]
        [HttpPut("{codigo}")]
        public async Task<ActionResult<Respuesta<bool>>> Actualizar([FromRoute] string codigo, [FromBody] DireccionSocioNegocioActualizarDTO obj)
        {
            var direccion = await _direccionApplication.ObtenerPorCodigoAsync(codigo);

            if (direccion.Dato == null)
            {
                direccion.Resultado = false;
                direccion.Mensaje = "Código de dirección no encontrado.";
                return NotFound(direccion);
            }

            var actualizar = await _direccionApplication.ActualizarAsync(codigo, obj);

            if (!actualizar.Resultado)
                return BadRequest(actualizar);

            return Ok(actualizar);
        }

        [RequierePermiso("SociosNegocio", Accion.Eliminar)]
        [HttpDelete("{codigo}")]
        public async Task<ActionResult<Respuesta<bool>>> Eliminar([FromRoute] string codigo)
        {
            var direccion = await _direccionApplication.ObtenerPorCodigoAsync(codigo);

            if (direccion.Dato == null)
            {
                direccion.Resultado = false;
                direccion.Mensaje = "Código de dirección no encontrado.";
                return NotFound(direccion);
            }

            var eliminar = await _direccionApplication.EliminarAsync(codigo);

            if (!eliminar.Resultado)
                return BadRequest(eliminar);

            return Ok(eliminar);
        }
    }
}
