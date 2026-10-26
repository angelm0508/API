using API.Application.DTO;
using API.Application.DTO.antiguedad;

namespace API.Application.Interface
{
    public interface IAntiguedadApplication
    {
        #region async methods
        /// <summary>Antigüedad de saldos por cobrar (clientes), al día de hoy.</summary>
        Task<Respuesta<List<FilaAntiguedadDTO>>> ObtenerCobrarAsync();

        /// <summary>Antigüedad de saldos por pagar (proveedores), al día de hoy.</summary>
        Task<Respuesta<List<FilaAntiguedadDTO>>> ObtenerPagarAsync();

        /// <summary>Documentos abiertos de un cliente; lista vacía si no hay o el socio no existe.</summary>
        Task<Respuesta<List<DocumentoAntiguedadDTO>>> ObtenerDetalleCobrarAsync(string codigoSn);

        /// <summary>Documentos abiertos de un proveedor; lista vacía si no hay o el socio no existe.</summary>
        Task<Respuesta<List<DocumentoAntiguedadDTO>>> ObtenerDetallePagarAsync(string codigoSn);
        #endregion
    }
}
