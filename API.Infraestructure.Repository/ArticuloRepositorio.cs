using API.Domain.Entity.Models;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Infraestructure.Repository
{
    public class ArticuloRepositorio : IRepositorioGenericoDos<Articulo>
    {
        private readonly ApiDbTestContext _contexto;

        public ArticuloRepositorio(ApiDbTestContext context)
        {
            _contexto = context;
        }

        #region async methods
        public async Task<bool> InsertarAsync(Articulo obj)
        {
            await _contexto.Articulos.AddAsync(obj);
            int creado = await _contexto.SaveChangesAsync();

            return creado > 0;
        }
        public async Task<bool> ActualizarAsync(string codigo, Articulo obj)
        {
            var articulo = await _contexto
                                    .Articulos
                                    .SingleOrDefaultAsync(x => x.Codigo == codigo);

            articulo.Nombre = obj.Nombre;
            articulo.CodigoGrupo = obj.CodigoGrupo;
            articulo.CodigoGrpMedida = obj.CodigoGrpMedida;
            articulo.FabricanteEntry = obj.FabricanteEntry;
            articulo.Activo = obj.Activo;
            articulo.ArticuloCompra = obj.ArticuloCompra;
            articulo.ArticuloVenta = obj.ArticuloVenta;
            articulo.ArticuloInventario = obj.ArticuloInventario;
            articulo.PrecioUnitario = obj.PrecioUnitario;
            articulo.CantDisponible = obj.PrecioUnitario;
            articulo.CantConfirmada = obj.CantConfirmada;
            articulo.CantPedida = obj.CantPedida;
            articulo.AlmacenDefecto = obj.AlmacenDefecto;
            articulo.NoApliDesc = obj.NoApliDesc;
            articulo.GestNoSerie = obj.GestNoSerie;
            articulo.GestLote = obj.GestLote;
            articulo.GestPorAlmacen = obj.GestPorAlmacen;
            articulo.Minimo = obj.Minimo;
            articulo.Maximo = obj.Maximo;
            articulo.Comentarios = obj.Comentarios;

            int actualizado = await _contexto
                                    .SaveChangesAsync();

            return actualizado > 0;
        }
        public async Task<bool> EliminarAsync(string sku)
        {
            var productoo = await _contexto
                                    .Articulos
                                    .SingleAsync(x => x.Codigo == sku);

            _contexto.Articulos.Remove(productoo);
            int eliminado = await _contexto.SaveChangesAsync();

            return eliminado > 0;
        }
        public async Task<Articulo> ObtenerAsync(string sku)
        {
            return await _contexto.Articulos
                                    .Include(x => x.CodigoGrupo)
                                    .Include(x => x.FabricanteEntry)
                                    .Include(x => x.CodigoGrpMedida)
                                    .FirstOrDefaultAsync(x => x.Codigo == sku);
        }
        public async Task<IQueryable<Articulo>> ObtenerTodoAsync()
        {
            return _contexto.Articulos;
        }
        #endregion
    }
}
