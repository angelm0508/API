using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    public class ExistenciaDomain : IExistenciaDomain
    {
        private readonly IRepositorioGenerico<ExistenciaArticulo, (string CodArticulo, string CodAlmacen)> _repo;

        public ExistenciaDomain(IRepositorioGenerico<ExistenciaArticulo, (string CodArticulo, string CodAlmacen)> repo)
        {
            _repo = repo;
        }

        public async Task<IEnumerable<ExistenciaArticulo>> ObtenerTodoAsync(string? articulo, string? almacen)
        {
            var q = await _repo.ObtenerTodoAsync();
            if (!string.IsNullOrWhiteSpace(articulo))
                q = q.Where(x => x.CodArticulo == articulo);
            if (!string.IsNullOrWhiteSpace(almacen))
                q = q.Where(x => x.CodAlmacen == almacen);
            return await q.ToListAsync();
        }

        public async Task<ExistenciaArticulo?> ObtenerAsync(string codArticulo, string codAlmacen)
        {
            var q = await _repo.ObtenerTodoAsync();
            return await q.FirstOrDefaultAsync(x => x.CodArticulo == codArticulo && x.CodAlmacen == codAlmacen);
        }

        public async Task<IEnumerable<ExistenciaArticulo>> ObtenerPorArticuloAsync(string codArticulo)
        {
            var q = await _repo.ObtenerTodoAsync();
            return await q.Where(x => x.CodArticulo == codArticulo).ToListAsync();
        }
    }
}
