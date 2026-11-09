using API.Domain.Entity.Models;

namespace API.Infraestructure.Repository
{
    public class ReporteVistaRepositorio : RepositorioGenericoEfCore<ReporteVista, int>
    {
        public ReporteVistaRepositorio(ApiDbTestContext contexto) : base(contexto) { }
    }
}
