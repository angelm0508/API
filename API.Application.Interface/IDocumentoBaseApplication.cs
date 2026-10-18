using API.Application.DTO;
using API.Application.DTO.documentoBase;

namespace API.Application.Interface
{
    public interface IDocumentoBaseApplication
    {
        Task<Respuesta<DocumentoBaseBorradorDTO>> ObtenerBorradorAsync(int tipoOrigen, int entry, int tipoDestino);
    }
}
