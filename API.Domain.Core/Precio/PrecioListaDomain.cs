using API.Domain.Core.Inventario;
using API.Domain.Entity.Models;
using API.Domain.Interface;

namespace API.Domain.Core.Precio
{
    public class PrecioListaDomain : IPrecioListaDomain
    {
        private const int MaxProfundidad = 10;

        private readonly IArticuloDomain _articulos;
        private readonly IListadoPrecioDomain _listas;

        public PrecioListaDomain(IArticuloDomain articulos, IListadoPrecioDomain listas)
        {
            _articulos = articulos;
            _listas = listas;
        }

        public async Task<PrecioResuelto> ResolverAsync(string codigoArticulo, int? listaPrecioEntry)
        {
            var articulo = await _articulos.ObtenerPorCodigoAsync(codigoArticulo)
                ?? throw new ArticuloNoExisteException(codigoArticulo);

            decimal precioBase = articulo.PrecioUnitario ?? 0m;

            if (listaPrecioEntry is null)
                return new PrecioResuelto(codigoArticulo, precioBase, 1m,
                    RedondeoCalculadora.Redondear(precioBase, null, null));

            var cadena = new List<ListadoPrecio>();
            var visitados = new HashSet<int>();
            int? actual = listaPrecioEntry;

            while (actual is not null)
            {
                if (!visitados.Add(actual.Value))
                    throw PrecioListaInvalidaException.Circular();
                if (cadena.Count >= MaxProfundidad)
                    throw PrecioListaInvalidaException.DemasiadoProfunda();

                var lp = await _listas.ObtenerAsync(actual.Value)
                    ?? throw PrecioListaInvalidaException.NoExiste(actual.Value);

                cadena.Add(lp);
                actual = lp.Base;
            }

            decimal factorEfectivo = 1m;
            foreach (var lp in cadena)
                factorEfectivo *= lp.Factor ?? 1m;

            var asignada = cadena[0];
            decimal precioLista = RedondeoCalculadora.Redondear(
                precioBase * factorEfectivo, asignada.MetodoRedondeo, asignada.ReglaRedondeo);

            return new PrecioResuelto(codigoArticulo, precioBase, factorEfectivo, precioLista);
        }
    }
}
