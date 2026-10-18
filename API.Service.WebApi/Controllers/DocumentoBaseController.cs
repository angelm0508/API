using API.Application.DTO;
using API.Application.DTO.documentoBase;
using API.Application.Interface;
using API.Domain.Interface;
using API.Service.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/DocumentoBase")]
    public class DocumentoBaseController : ControllerBase
    {
        private readonly IDocumentoBaseApplication _documentoBaseApplication;

        public DocumentoBaseController(IDocumentoBaseApplication documentoBaseApplication)
        {
            _documentoBaseApplication = documentoBaseApplication;
        }

        // Módulo de permisos de cada tipo de documento de venta.
        private static string? ModuloDe(int tipo) => tipo switch
        {
            TipoDocumentoVenta.Cotizacion => "Cotizaciones",
            TipoDocumentoVenta.Pedido => "Pedidos",
            TipoDocumentoVenta.Entrega => "Entregas",
            TipoDocumentoVenta.Factura => "Facturas",
            _ => null
        };

        /// <summary>
        /// Borrador del documento destino con las líneas abiertas del origen ("Copiar a"). Exige Ver en el módulo del
        /// origen y Agregar en el del destino; se evalúa aquí (y no con el atributo) porque los módulos dependen de
        /// los parámetros.
        /// </summary>
        [HttpGet("{tipoOrigen:int}/{entry:int}/Abierto")]
        public async Task<ActionResult<Respuesta<DocumentoBaseBorradorDTO>>> ObtenerAbiertoAsync(
            [FromRoute] int tipoOrigen, [FromRoute] int entry, [FromQuery] int destino)
        {
            var moduloOrigen = ModuloDe(tipoOrigen);
            var moduloDestino = ModuloDe(destino);
            if (moduloOrigen is null || moduloDestino is null)
                return BadRequest(new Respuesta<DocumentoBaseBorradorDTO> { Mensaje = "El tipo de documento no es válido." });

            if (!PermisoUsuario.Tiene(User, moduloOrigen, Accion.Ver) || !PermisoUsuario.Tiene(User, moduloDestino, Accion.Agregar))
                return PermisoUsuario.Denegado();

            var respuesta = await _documentoBaseApplication.ObtenerBorradorAsync(tipoOrigen, entry, destino);
            if (respuesta.Resultado)
                return Ok(respuesta);

            return respuesta.Mensaje == DocumentoBaseMensajes.OrigenNoExiste ? NotFound(respuesta) : BadRequest(respuesta);
        }
    }
}
