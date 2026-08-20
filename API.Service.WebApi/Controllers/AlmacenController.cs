using API.Application.DTO.almacen;
using API.Application.Interface;
using API.Transversal.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/Almacen")]
    public class AlmacenController : ControllerBase
    {
        private readonly IAlmacenApplication _almacenApplication;

        public AlmacenController(IAlmacenApplication almacenApplication)
        {
            _almacenApplication = almacenApplication;
        }

        [HttpGet("{codigo}")]
        public async Task<ActionResult<AlmacenDTO>> ObtenerPorCodigo([FromRoute] string codigo)
        {
            var almacen = await _almacenApplication.ObtenerPorCodigoAsync(codigo);

            if (!almacen.Resultado)
                return BadRequest(new RespuestaError(almacen.Mensaje));

            if (almacen.Dato == null)
                return NotFound(new RespuestaError("Código de almacén no encontrado."));

            return Ok(almacen.Dato);
        }

        [HttpGet("Nombre/{nombre}")]
        public async Task<ActionResult<AlmacenDTO>> ObtenerPorNombre([FromRoute] string nombre)
        {
            var almacen = await _almacenApplication.ObtenerPorNombreAsync(nombre);

            if (!almacen.Resultado)
                return BadRequest(new RespuestaError(almacen.Mensaje));

            if (almacen.Dato == null)
                return NotFound(new RespuestaError("Nombre de almacén no encontrado."));

            return Ok(almacen.Dato);
        }

        [HttpGet("ContengaNombre/{nombre}")]
        public async Task<ActionResult<List<AlmacenDTO>>> ObtenerContengaNombre([FromRoute] string nombre)
        {
            var almacenes = await _almacenApplication.ObtenerContengaNombreAsync(nombre);

            if (!almacenes.Resultado)
                return BadRequest(new RespuestaError(almacenes.Mensaje));

            return Ok(almacenes.Dato);
        }

        [HttpGet("ContengaCodigo/{codigo}")]
        public async Task<ActionResult<List<AlmacenDTO>>> ObtenerContengaCodigo([FromRoute] string codigo)
        {
            var almacenes = await _almacenApplication.ObtenerContengaCodigoAsync(codigo);

            if (!almacenes.Resultado)
                return BadRequest(new RespuestaError(almacenes.Mensaje));

            return Ok(almacenes.Dato);
        }

        [HttpGet()]
        public async Task<ActionResult<List<AlmacenDTO>>> ObtenerTodo()
        {
            var almacenes = await _almacenApplication.ObtenerAsync();

            if (!almacenes.Resultado)
                return BadRequest(new RespuestaError(almacenes.Mensaje));

            return Ok(almacenes.Dato);
        }

        [HttpPost]
        public async Task<ActionResult> Crear([FromBody] AlmacenCrearDTO obj)
        {
            var insertar = await _almacenApplication.InsertarAsync(obj);

            if (!insertar.Resultado)
                return BadRequest(new RespuestaError(insertar.Mensaje));

            return Ok();
        }

        [HttpPut("{codigo}")]
        public async Task<ActionResult> Actualizar([FromRoute] string codigo, [FromBody] AlmacenActualizarDTO obj)
        {
            var almacen = await _almacenApplication.ObtenerPorCodigoAsync(codigo);

            if (almacen.Dato == null)
                return NotFound(new RespuestaError("Código de almacén no encontrado."));

            var actualizar = await _almacenApplication.ActualizarAsync(codigo, obj);

            if (!actualizar.Resultado)
                return BadRequest(new RespuestaError(actualizar.Mensaje));

            return Ok();
        }

        [HttpDelete("{codigo}")]
        public async Task<ActionResult> Eliminar([FromRoute] string codigo)
        {
            var almacen = await _almacenApplication.ObtenerPorCodigoAsync(codigo);

            if (almacen.Dato == null)
                return NotFound(new RespuestaError("Código de almacén no encontrado."));

            var eliminar = await _almacenApplication.EliminarAsync(codigo);

            if (!eliminar.Resultado)
                return BadRequest(new RespuestaError($"{eliminar.Mensaje}"));

            return Ok();
        }
    }
}
