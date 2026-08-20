using API.Domain.Entity.Models;

namespace API.Infraestructure.Repository
{
    public class NumeracionDocumentoDetRepositorio : RepositorioGenericoEfCore<NumeracionDocumentoDet, int>
    {
        public NumeracionDocumentoDetRepositorio(ApiDbTestContext contexto) : base(contexto) { }
    }
}
