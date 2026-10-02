using API.Application.DTO;
using API.Application.DTO.articulo.unidad_medida_articulo;
using API.Application.Interface;
using API.Domain.Interface;
using API.Service.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/UnidadMedidaArticulo")]
    public class UnidadMedidaArticuloController : ControllerBase
    {
        private readonly IUnidadMedidaArticuloApplication _unidadMedidaArticuloApplication;

        public UnidadMedidaArticuloController(IUnidadMedidaArticuloApplication unidadMedidaArticuloApplication)
        {
            _unidadMedidaArticuloApplication = unidadMedidaArticuloApplication;
        }

        [RequierePermiso("UnidadesMedida", Accion.Ver)]
        [HttpGet("{id:int}")]
        public async Task<ActionResult<Respuesta<UnidadMedidaArticuloDTO>>> Obtener([FromRoute] int id)
        {
            var medida = await _unidadMedidaArticuloApplication.ObtenerAsync(id);

            if (!medida.Resultado)
            {
                return BadRequest(medida);
            }

            if (medida.Dato == null)
            {
                medida.Resultado = false;
                medida.Mensaje = "El código de la medida no se encontró.";
                return NotFound(medida);
            }

            return Ok(medida);
        }

        [RequierePermiso("UnidadesMedida", Accion.Ver)]
        [HttpGet("PorCodigo/{codigo}")]
        public async Task<ActionResult<Respuesta<UnidadMedidaArticuloDTO>>> ObtenerPorCodigo([FromRoute] string codigo)
        {
            var medida = await _unidadMedidaArticuloApplication.ObtenerAsync(codigo);

            if (!medida.Resultado)
            {
                return BadRequest(medida);
            }

            if (medida.Dato == null)
            {
                medida.Resultado = false;
                medida.Mensaje = "El código de la medida no se encontró.";
                return NotFound(medida);
            }

            return Ok(medida);
        }

        [RequierePermiso("UnidadesMedida", Accion.Ver)]
        [HttpGet("Contenga/{name}")]
        public async Task<ActionResult<Respuesta<IEnumerable<UnidadMedidaArticuloDTO>>>> ObteneContengaNombreAsync([FromRoute] string name)
        {
            var medidas = await _unidadMedidaArticuloApplication.ObtenerContengaNombreAsync(name);

            if (!medidas.Resultado)
            {
                return BadRequest(medidas);
            }

            return Ok(medidas);
        }

        [RequierePermiso("UnidadesMedida", Accion.Ver)]
        [HttpGet]
        public async Task<ActionResult<Respuesta<IEnumerable<UnidadMedidaArticuloDTO>>>> ObtenerTodoAsync()
        {
            var medidas = await _unidadMedidaArticuloApplication.ObtenerTodoAsync();

            if (!medidas.Resultado)
            {
                return BadRequest(medidas);
            }

            return Ok(medidas);
        }

        [RequierePermiso("UnidadesMedida", Accion.Agregar)]
        [HttpPost]
        public async Task<ActionResult<Respuesta<int>>> InsertarAsync([FromBody] UnidadMedidaArticuloCrearDTO obj)
        {
            var insert = await _unidadMedidaArticuloApplication.InsertarAsync(obj);

            if (!insert.Resultado)
            {
                return BadRequest(insert);
            }

            return Ok(insert);
        }

        [RequierePermiso("UnidadesMedida", Accion.Actualizar)]
        [HttpPut("{id:int}")]
        public async Task<ActionResult<Respuesta<bool>>> ActualizarAsync([FromRoute] int id, [FromBody] UnidadMedidaArticuloActualizarDTO obj)
        {
            var medida = await _unidadMedidaArticuloApplication.ObtenerAsync(id);

            if (medida.Dato == null)
            {
                medida.Resultado = false;
                medida.Mensaje = "El código de la medida no se encontró.";
                return NotFound(medida);
            }

            var update = await _unidadMedidaArticuloApplication.ActualizarAsync(id, obj);

            if (!update.Resultado)
            {
                return BadRequest(update);
            }

            return Ok(update);
        }

        [RequierePermiso("UnidadesMedida", Accion.Eliminar)]
        [HttpDelete("{id:int}")]
        public async Task<ActionResult<Respuesta<bool>>> EliminarAsync([FromRoute] int id)
        {
            var medida = await _unidadMedidaArticuloApplication.ObtenerAsync(id);

            if (medida.Dato == null)
            {
                medida.Resultado = false;
                medida.Mensaje = "El código de la medida no se encontró.";
                return NotFound(medida);
            }

            var delete = await _unidadMedidaArticuloApplication.EliminarAsync(id);

            if (!delete.Resultado)
            {
                return BadRequest(delete);
            }

            return Ok(delete);
        }
    }
}
