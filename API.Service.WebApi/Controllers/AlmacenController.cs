using API.Application.DTO;
using API.Application.DTO.almacen;
using API.Application.Interface;
using API.Domain.Interface;
using API.Service.WebApi.Authorization;
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

        [RequierePermiso("Almacenes", Accion.Ver)]
        [HttpGet("{codigo}")]
        public async Task<ActionResult<Respuesta<AlmacenDTO>>> ObtenerPorCodigo([FromRoute] string codigo)
        {
            var almacen = await _almacenApplication.ObtenerPorCodigoAsync(codigo);

            if (!almacen.Resultado)
                return BadRequest(almacen);

            if (almacen.Dato == null)
            {
                almacen.Resultado = false;
                almacen.Mensaje = "Código de almacén no encontrado.";
                return NotFound(almacen);
            }

            return Ok(almacen);
        }

        [RequierePermiso("Almacenes", Accion.Ver)]
        [HttpGet("Nombre/{nombre}")]
        public async Task<ActionResult<Respuesta<AlmacenDTO>>> ObtenerPorNombre([FromRoute] string nombre)
        {
            var almacen = await _almacenApplication.ObtenerPorNombreAsync(nombre);

            if (!almacen.Resultado)
                return BadRequest(almacen);

            if (almacen.Dato == null)
            {
                almacen.Resultado = false;
                almacen.Mensaje = "Nombre de almacén no encontrado.";
                return NotFound(almacen);
            }

            return Ok(almacen);
        }

        [RequierePermiso("Almacenes", Accion.Ver)]
        [HttpGet("ContengaNombre/{nombre}")]
        public async Task<ActionResult<Respuesta<IEnumerable<AlmacenDTO>>>> ObtenerContengaNombre([FromRoute] string nombre)
        {
            var almacenes = await _almacenApplication.ObtenerContengaNombreAsync(nombre);

            if (!almacenes.Resultado)
                return BadRequest(almacenes);

            return Ok(almacenes);
        }

        [RequierePermiso("Almacenes", Accion.Ver)]
        [HttpGet("ContengaCodigo/{codigo}")]
        public async Task<ActionResult<Respuesta<IEnumerable<AlmacenDTO>>>> ObtenerContengaCodigo([FromRoute] string codigo)
        {
            var almacenes = await _almacenApplication.ObtenerContengaCodigoAsync(codigo);

            if (!almacenes.Resultado)
                return BadRequest(almacenes);

            return Ok(almacenes);
        }

        [RequierePermiso("Almacenes", Accion.Ver)]
        [HttpGet()]
        public async Task<ActionResult<Respuesta<IEnumerable<AlmacenDTO>>>> ObtenerTodo()
        {
            var almacenes = await _almacenApplication.ObtenerAsync();

            if (!almacenes.Resultado)
                return BadRequest(almacenes);

            return Ok(almacenes);
        }

        [RequierePermiso("Almacenes", Accion.Agregar)]
        [HttpPost]
        public async Task<ActionResult<Respuesta<bool>>> Crear([FromBody] AlmacenCrearDTO obj)
        {
            var insertar = await _almacenApplication.InsertarAsync(obj);

            if (!insertar.Resultado)
                return BadRequest(insertar);

            return Ok(insertar);
        }

        [RequierePermiso("Almacenes", Accion.Actualizar)]
        [HttpPut("{codigo}")]
        public async Task<ActionResult<Respuesta<bool>>> Actualizar([FromRoute] string codigo, [FromBody] AlmacenActualizarDTO obj)
        {
            var almacen = await _almacenApplication.ObtenerPorCodigoAsync(codigo);

            if (almacen.Dato == null)
            {
                almacen.Resultado = false;
                almacen.Mensaje = "Código de almacén no encontrado.";
                return NotFound(almacen);
            }

            var actualizar = await _almacenApplication.ActualizarAsync(codigo, obj);

            if (!actualizar.Resultado)
                return BadRequest(actualizar);

            return Ok(actualizar);
        }

        [RequierePermiso("Almacenes", Accion.Eliminar)]
        [HttpDelete("{codigo}")]
        public async Task<ActionResult<Respuesta<bool>>> Eliminar([FromRoute] string codigo)
        {
            var almacen = await _almacenApplication.ObtenerPorCodigoAsync(codigo);

            if (almacen.Dato == null)
            {
                almacen.Resultado = false;
                almacen.Mensaje = "Código de almacén no encontrado.";
                return NotFound(almacen);
            }

            var eliminar = await _almacenApplication.EliminarAsync(codigo);

            if (!eliminar.Resultado)
                return BadRequest(eliminar);

            return Ok(eliminar);
        }
    }
}
