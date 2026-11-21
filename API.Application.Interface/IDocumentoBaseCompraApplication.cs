using API.Application.DTO;
using API.Application.DTO.documentoBase;

namespace API.Application.Interface
{
    public interface IDocumentoBaseCompraApplication
    {
        Task<Respuesta<DocumentoBaseBorradorDTO>> ObtenerBorradorAsync(int tipoOrigen, int entry, int tipoDestino);
    }
}
