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
    [Route("api/Modulo")]
    public class ModuloController : ControllerBase
    {
        private readonly IPermisoApplication _permisoApplication;

        public ModuloController(IPermisoApplication permisoApplication)
        {
            _permisoApplication = permisoApplication;
        }

        [HttpGet]
        [RequierePermiso("Usuarios", Accion.Ver)]
        public async Task<ActionResult<Respuesta<IEnumerable<ModuloDTO>>>> Obtener()
        {
            var res = await _permisoApplication.ObtenerModulosAsync();
            return res.Resultado ? Ok(res) : BadRequest(res);
        }
    }
}
