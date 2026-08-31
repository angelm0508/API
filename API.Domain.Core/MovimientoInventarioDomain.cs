using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    public class MovimientoInventarioDomain : IMovimientoInventarioDomain
    {
        private readonly IRepositorioGenerico<MovimientoInventario, int> _repo;

        public MovimientoInventarioDomain(IRepositorioGenerico<MovimientoInventario, int> repo)
        {
            _repo = repo;
        }

        public async Task<IEnumerable<MovimientoInventario>> ObtenerPorArticuloAsync(string codArticulo, string? almacen, DateTime? desde, DateTime? hasta)
        {
            var q = await _repo.ObtenerTodoAsync();
            q = q.Where(x => x.CodArticulo == codArticulo);
            if (!string.IsNullOrWhiteSpace(almacen))
                q = q.Where(x => x.CodAlmacen == almacen);
            if (desde.HasValue)
                q = q.Where(x => x.Fecha >= desde.Value);
            if (hasta.HasValue)
                q = q.Where(x => x.Fecha <= hasta.Value);
            return await q.OrderBy(x => x.Fecha).ThenBy(x => x.Entry).ToListAsync();
        }
    }
}
