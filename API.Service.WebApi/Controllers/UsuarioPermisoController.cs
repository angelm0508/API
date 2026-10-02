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
    [Route("api/UsuarioPermiso")]
    public class UsuarioPermisoController : ControllerBase
    {
        private readonly IPermisoApplication _permisoApplication;

        public UsuarioPermisoController(IPermisoApplication permisoApplication)
        {
            _permisoApplication = permisoApplication;
        }

        [HttpGet("{usuarioId:int}")]
        [RequierePermiso("Usuarios", Accion.Ver)]
        public async Task<ActionResult<Respuesta<IEnumerable<PermisoModuloDTO>>>> Obtener([FromRoute] int usuarioId)
        {
            var res = await _permisoApplication.ObtenerMatrizUsuarioAsync(usuarioId);
            return res.Resultado ? Ok(res) : BadRequest(res);
        }

        [HttpPut("{usuarioId:int}")]
        [RequierePermiso("Usuarios", Accion.Actualizar)]
        public async Task<ActionResult<Respuesta<bool>>> Guardar([FromRoute] int usuarioId, [FromBody] List<PermisoModuloDTO> matriz)
        {
            if (User.FindFirst("SuperUsuario")?.Value != "S")
                return Forbid();

            var res = await _permisoApplication.GuardarMatrizUsuarioAsync(usuarioId, matriz);
            return res.Resultado ? Ok(res) : BadRequest(res);
        }

        [HttpPost("{usuarioId:int}/AplicarPlantilla/{plantillaId:int}")]
        [RequierePermiso("Usuarios", Accion.Actualizar)]
        public async Task<ActionResult<Respuesta<bool>>> AplicarPlantilla([FromRoute] int usuarioId, [FromRoute] int plantillaId)
        {
            if (User.FindFirst("SuperUsuario")?.Value != "S")
                return Forbid();

            var res = await _permisoApplication.AplicarPlantillaAsync(usuarioId, plantillaId);
            return res.Resultado ? Ok(res) : BadRequest(res);
        }
    }
}
