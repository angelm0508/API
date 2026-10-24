using API.Domain.Entity.Models;

namespace API.Infraestructure.Repository
{
    public class FormaPagoRepositorio : RepositorioGenericoEfCore<FormaPago, string>
    {
        public FormaPagoRepositorio(ApiDbTestContext contexto) : base(contexto) { }
    }
}
