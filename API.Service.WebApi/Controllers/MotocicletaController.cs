using API.Application.DTO;
using API.Application.DTO.motocicleta;
using API.Application.DTO.ordenServicio;
using API.Application.Interface;
using API.Domain.Interface;
using API.Service.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/Motocicleta")]
    public class MotocicletaController : ControllerBase
    {
        private readonly IMotocicletaApplication _application;

        public MotocicletaController(IMotocicletaApplication application)
        {
            _application = application;
        }

        [RequierePermiso("Motocicletas", Accion.Ver)]
        [HttpGet]
        public async Task<ActionResult<Respuesta<IEnumerable<MotocicletaDTO>>>> Buscar([FromQuery] string? codigoSn, [FromQuery] string? placa)
        {
            var motos = await _application.BuscarAsync(codigoSn, placa);

            if (!motos.Resultado)
                return BadRequest(motos);

            return Ok(motos);
        }

        [RequierePermiso("Motocicletas", Accion.Ver)]
        [HttpGet("{entry:int}")]
        public async Task<ActionResult<Respuesta<MotocicletaDTO>>> Obtener([FromRoute] int entry)
        {
            var moto = await _application.ObtenerAsync(entry);

            if (!moto.Resultado)
                return BadRequest(moto);

            if (moto.Dato == null)
            {
                moto.Resultado = false;
                moto.Mensaje = "La motocicleta no se encontró.";
                return NotFound(moto);
            }

            return Ok(moto);
        }

        // Muestra órdenes de servicio: además de ver motocicletas exige ver órdenes de servicio.
        [RequierePermiso("Motocicletas", Accion.Ver)]
        [HttpGet("{entry:int}/Historial")]
        public async Task<ActionResult<Respuesta<IEnumerable<OrdenServicioDTO>>>> ObtenerHistorial([FromRoute] int entry)
        {
            if (!PermisoUsuario.Tiene(User, "OrdenesServicio", Accion.Ver))
                return PermisoUsuario.Denegado();

            var historial = await _application.ObtenerHistorialAsync(entry);

            if (!historial.Resultado)
                return BadRequest(historial);

            return Ok(historial);
        }

        [RequierePermiso("Motocicletas", Accion.Agregar)]
        [HttpPost]
        public async Task<ActionResult<Respuesta<int>>> Crear([FromBody] MotocicletaCrearDTO obj)
        {
            var insertar = await _application.InsertarAsync(obj);

            if (!insertar.Resultado)
                return BadRequest(insertar);

            return Ok(insertar);
        }

        [RequierePermiso("Motocicletas", Accion.Actualizar)]
        [HttpPut("{entry:int}")]
        public async Task<ActionResult<Respuesta<bool>>> Actualizar([FromRoute] int entry, [FromBody] MotocicletaActualizarDTO obj)
        {
            var moto = await _application.ObtenerAsync(entry);

            if (moto.Dato == null)
            {
                moto.Resultado = false;
                moto.Mensaje = "La motocicleta no se encontró.";
                return NotFound(moto);
            }

            var actualizar = await _application.ActualizarAsync(entry, obj);

            if (!actualizar.Resultado)
                return BadRequest(actualizar);

            return Ok(actualizar);
        }

        [RequierePermiso("Motocicletas", Accion.Eliminar)]
        [HttpDelete("{entry:int}")]
        public async Task<ActionResult<Respuesta<bool>>> Eliminar([FromRoute] int entry)
        {
            var moto = await _application.ObtenerAsync(entry);

            if (moto.Dato == null)
            {
                moto.Resultado = false;
                moto.Mensaje = "La motocicleta no se encontró.";
                return NotFound(moto);
            }

            var eliminar = await _application.EliminarAsync(entry);

            if (!eliminar.Resultado)
                return BadRequest(eliminar);

            return Ok(eliminar);
        }
    }
}
