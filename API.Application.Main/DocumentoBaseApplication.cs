using API.Application.DTO;
using API.Application.DTO.documentoBase;
using API.Application.Interface;
using API.Domain.Interface;

namespace API.Application.Main
{
    public class DocumentoBaseApplication : IDocumentoBaseApplication
    {
        private readonly IDocumentoBaseService _documentoBase;

        public DocumentoBaseApplication(IDocumentoBaseService documentoBase)
        {
            _documentoBase = documentoBase;
        }

        public async Task<Respuesta<DocumentoBaseBorradorDTO>> ObtenerBorradorAsync(int tipoOrigen, int entry, int tipoDestino)
        {
            var respuesta = new Respuesta<DocumentoBaseBorradorDTO>();
            try
            {
                var b = await _documentoBase.ObtenerBorradorAsync(tipoOrigen, entry, tipoDestino);
                respuesta.Dato = new DocumentoBaseBorradorDTO
                {
                    OrigenTipo = b.OrigenTipo,
                    OrigenEntry = b.OrigenEntry,
                    OrigenNumDoc = b.OrigenNumDoc,
                    Cabecera = new DocumentoBaseCabeceraDTO
                    {
                        CodigoSn = b.Cabecera.CodigoSn,
                        NombreSn = b.Cabecera.NombreSn,
                        Direccion = b.Cabecera.Direccion,
                        MonedaDoc = b.Cabecera.MonedaDoc,
                        PrctjeDesc = b.Cabecera.PrctjeDesc,
                        PrctjeImpuesto = b.Cabecera.PrctjeImpuesto,
                        Comentario = b.Cabecera.Comentario
                    },
                    Lineas = b.Lineas.Select(l => new DocumentoBaseLineaDTO
                    {
                        BaseTipo = l.BaseTipo,
                        BaseEntry = l.BaseEntry,
                        BaseLinea = l.BaseLinea,
                        CodArticulo = l.CodArticulo,
                        Descripcion = l.Descripcion,
                        CantidadOrigen = l.CantidadOrigen,
                        CantidadAbierta = l.CantidadAbierta,
                        Precio = l.Precio,
                        PrecioBruto = l.PrecioBruto,
                        PrctjeDesc = l.PrctjeDesc,
                        CodigoImpuesto = l.CodigoImpuesto,
                        Impuesto = l.Impuesto,
                        CodAlmacen = l.CodAlmacen
                    }).ToList()
                };
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }
    }
}
