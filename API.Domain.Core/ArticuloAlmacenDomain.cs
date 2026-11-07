using API.Domain.Core.Inventario;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    /// <summary>
    /// Configuración de stock por artículo y almacén (mínimo, máximo y bloqueo de salidas) y los
    /// cálculos de la ficha de existencias por almacén. Los avisos (mínimo/máximo) solo informan.
    /// </summary>
    public class ArticuloAlmacenDomain : IArticuloAlmacenDomain
    {
        private const string Si = "S";
        private const string No = "N";
        private const int DecimalesMaximos = 6;

        private readonly IRepositorioGenerico<ArticuloAlmacen, (string CodArticulo, string CodAlmacen)> _repoConfig;
        private readonly IRepositorioGenerico<Articulo, string> _repoArticulo;
        private readonly IRepositorioGenerico<Almacen, string> _repoAlmacen;
        private readonly IRepositorioGenerico<ExistenciaArticulo, (string CodArticulo, string CodAlmacen)> _repoExistencia;
        private readonly IEjecutorTransaccion _tx;

        public ArticuloAlmacenDomain(
            IRepositorioGenerico<ArticuloAlmacen, (string CodArticulo, string CodAlmacen)> repoConfig,
            IRepositorioGenerico<Articulo, string> repoArticulo,
            IRepositorioGenerico<Almacen, string> repoAlmacen,
            IRepositorioGenerico<ExistenciaArticulo, (string CodArticulo, string CodAlmacen)> repoExistencia,
            IEjecutorTransaccion tx)
        {
            _repoConfig = repoConfig;
            _repoArticulo = repoArticulo;
            _repoAlmacen = repoAlmacen;
            _repoExistencia = repoExistencia;
            _tx = tx;
        }

        public async Task<IReadOnlyList<ArticuloAlmacenFila>> ObtenerFilasAsync(string codArticulo)
        {
            if (await _repoArticulo.ObtenerAsync(codArticulo) is null)
                throw new ArticuloNoExisteException(codArticulo);

            var configuraciones = await (await _repoConfig.ObtenerTodoAsync())
                .Where(c => c.CodArticulo == codArticulo)
                .ToListAsync();
            var existencias = await (await _repoExistencia.ObtenerTodoAsync())
                .Where(e => e.CodArticulo == codArticulo)
                .ToListAsync();

            // Activos siempre; inactivos solo si el artículo tiene configuración o existencias en ellos.
            var conDatos = configuraciones.Select(c => c.CodAlmacen)
                .Concat(existencias.Select(e => e.CodAlmacen))
                .Distinct()
                .ToList();
            var almacenes = await (await _repoAlmacen.ObtenerTodoAsync())
                .Where(a => a.Activo == Si || conDatos.Contains(a.Codigo))
                .ToListAsync();

            var configPorAlmacen = configuraciones.ToDictionary(c => c.CodAlmacen);
            var existenciaPorAlmacen = existencias.ToDictionary(e => e.CodAlmacen);

            return almacenes
                .OrderBy(a => a.Codigo, StringComparer.Ordinal)
                .Select(a =>
                {
                    configPorAlmacen.TryGetValue(a.Codigo, out var config);
                    existenciaPorAlmacen.TryGetValue(a.Codigo, out var existencia);

                    var enStock = existencia?.Disponible ?? 0m;
                    var comprometido = existencia?.Comprometido ?? 0m;
                    var pedido = existencia?.Pedido ?? 0m;
                    var disponible = StockAlmacenCalculadora.Disponible(enStock, comprometido, pedido);

                    return new ArticuloAlmacenFila(
                        a.Codigo,
                        a.Nombre ?? string.Empty,
                        config?.Bloqueado == Si,
                        enStock,
                        comprometido,
                        pedido,
                        disponible,
                        config?.Minimo,
                        config?.Maximo,
                        StockAlmacenCalculadora.NivelNecesario(config?.Maximo, disponible),
                        StockAlmacenCalculadora.Estado(enStock, config?.Minimo, config?.Maximo));
                })
                .ToList();
        }

        public async Task GuardarAsync(string codArticulo, IReadOnlyList<ArticuloAlmacenConfig> filas)
        {
            // Se valida TODO antes de tocar el repositorio de configuración o abrir la transacción.
            if (await _repoArticulo.ObtenerAsync(codArticulo) is null)
                throw new ArticuloNoExisteException(codArticulo);

            var codigos = filas.Select(f => f.CodAlmacen).Distinct().ToList();
            var almacenes = await (await _repoAlmacen.ObtenerTodoAsync())
                .Where(a => codigos.Contains(a.Codigo))
                .ToListAsync();
            var codigoCanonico = almacenes.ToDictionary(a => a.Codigo, a => a.Codigo, StringComparer.OrdinalIgnoreCase);

            // Cero es "sin valor": se normaliza a NULL. Una fila sin mínimo, sin máximo y sin bloqueo no se guarda.
            var deseadas = new Dictionary<string, ArticuloAlmacenConfig>(StringComparer.OrdinalIgnoreCase);
            foreach (var fila in filas)
            {
                if (deseadas.ContainsKey(fila.CodAlmacen))
                    throw new Exception($"El almacén {fila.CodAlmacen} está repetido en la lista.");
                if (!codigoCanonico.TryGetValue(fila.CodAlmacen, out var codAlmacen))
                    throw new AlmacenNoExisteException(fila.CodAlmacen);

                if (fila.Minimo is < 0)
                    throw new Exception($"El stock mínimo del almacén {codAlmacen} no puede ser negativo.");
                if (fila.Maximo is < 0)
                    throw new Exception($"El stock máximo del almacén {codAlmacen} no puede ser negativo.");
                if (ExcedeDecimales(fila.Minimo))
                    throw new Exception($"El stock mínimo del almacén {codAlmacen} admite como máximo {DecimalesMaximos} decimales.");
                if (ExcedeDecimales(fila.Maximo))
                    throw new Exception($"El stock máximo del almacén {codAlmacen} admite como máximo {DecimalesMaximos} decimales.");

                var minimo = fila.Minimo is 0 ? null : fila.Minimo;
                var maximo = fila.Maximo is 0 ? null : fila.Maximo;
                if (minimo.HasValue && maximo.HasValue && maximo.Value < minimo.Value)
                    throw new Exception($"El stock máximo del almacén {codAlmacen} no puede ser menor que el mínimo.");

                // Se registra tambien lo descartado para detectar repetidos aunque la fila quede vacía.
                deseadas[codAlmacen] = new ArticuloAlmacenConfig(codAlmacen, minimo, maximo, fila.Bloqueado);
            }

            await _tx.EjecutarAsync(async () =>
            {
                var existentes = await (await _repoConfig.ObtenerTodoAsync())
                    .Where(c => c.CodArticulo == codArticulo)
                    .ToListAsync();

                // Primero actualizaciones y bajas (cada una guarda dentro de la transacción) y al final las altas,
                // que el ejecutor persiste junto con el commit.
                var yaExistentes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var existente in existentes)
                {
                    yaExistentes.Add(existente.CodAlmacen);
                    if (deseadas.TryGetValue(existente.CodAlmacen, out var deseada) && TieneContenido(deseada))
                        await _repoConfig.ActualizarAsync((codArticulo, existente.CodAlmacen), Convertir(codArticulo, deseada));
                    else
                        await _repoConfig.EliminarAsync((codArticulo, existente.CodAlmacen));
                }

                foreach (var deseada in deseadas.Values.Where(d => TieneContenido(d) && !yaExistentes.Contains(d.CodAlmacen)))
                    await _repoConfig.AgregarSinGuardarAsync(Convertir(codArticulo, deseada));

                return true;
            });
        }

        public async Task<IReadOnlyDictionary<(string CodArticulo, string CodAlmacen), ArticuloAlmacenConfig>> ObtenerConfiguracionAsync(string? articulo, string? almacen)
        {
            var q = await _repoConfig.ObtenerTodoAsync();
            if (!string.IsNullOrWhiteSpace(articulo))
                q = q.Where(c => c.CodArticulo == articulo);
            if (!string.IsNullOrWhiteSpace(almacen))
                q = q.Where(c => c.CodAlmacen == almacen);

            var filas = await q.ToListAsync();
            return filas.ToDictionary(
                c => (c.CodArticulo, c.CodAlmacen),
                c => new ArticuloAlmacenConfig(c.CodAlmacen, c.Minimo, c.Maximo, c.Bloqueado == Si));
        }

        public async Task<IReadOnlyList<AlmacenBajoMinimo>> ObtenerBajoMinimoAsync()
        {
            var configuraciones = await (await _repoConfig.ObtenerTodoAsync())
                .Where(c => c.Minimo > 0)
                .ToListAsync();
            if (configuraciones.Count == 0)
                return new List<AlmacenBajoMinimo>();

            var codArticulos = configuraciones.Select(c => c.CodArticulo).Distinct().ToList();
            var codAlmacenes = configuraciones.Select(c => c.CodAlmacen).Distinct().ToList();

            var articulos = (await (await _repoArticulo.ObtenerTodoAsync())
                    .Where(a => a.Activo == Si && a.ArticuloInventario == Si && codArticulos.Contains(a.Codigo))
                    .ToListAsync())
                .ToDictionary(a => a.Codigo);
            var almacenes = (await (await _repoAlmacen.ObtenerTodoAsync())
                    .Where(a => a.Activo == Si && codAlmacenes.Contains(a.Codigo))
                    .ToListAsync())
                .ToDictionary(a => a.Codigo);
            var existencias = (await (await _repoExistencia.ObtenerTodoAsync())
                    .Where(e => codArticulos.Contains(e.CodArticulo))
                    .ToListAsync())
                .ToDictionary(e => (e.CodArticulo, e.CodAlmacen), e => e.Disponible);

            var resultado = new List<AlmacenBajoMinimo>();
            foreach (var c in configuraciones
                .OrderBy(c => c.CodArticulo, StringComparer.Ordinal)
                .ThenBy(c => c.CodAlmacen, StringComparer.Ordinal))
            {
                if (!articulos.TryGetValue(c.CodArticulo, out var articulo) || !almacenes.TryGetValue(c.CodAlmacen, out var almacen))
                    continue;

                var enStock = existencias.TryGetValue((c.CodArticulo, c.CodAlmacen), out var disponible) ? disponible : 0m;
                if (StockAlmacenCalculadora.Estado(enStock, c.Minimo, null) != StockAlmacenCalculadora.BajoMinimo)
                    continue;

                resultado.Add(new AlmacenBajoMinimo(
                    c.CodArticulo,
                    articulo.Nombre ?? string.Empty,
                    c.CodAlmacen,
                    almacen.Nombre ?? string.Empty,
                    enStock,
                    c.Minimo!.Value));
            }
            return resultado;
        }

        private static bool ExcedeDecimales(decimal? valor) =>
            valor.HasValue && Math.Round(valor.Value, DecimalesMaximos) != valor.Value;

        private static bool TieneContenido(ArticuloAlmacenConfig config) =>
            config.Minimo.HasValue || config.Maximo.HasValue || config.Bloqueado;

        private static ArticuloAlmacen Convertir(string codArticulo, ArticuloAlmacenConfig config) => new()
        {
            CodArticulo = codArticulo,
            CodAlmacen = config.CodAlmacen,
            Minimo = config.Minimo,
            Maximo = config.Maximo,
            Bloqueado = config.Bloqueado ? Si : No
        };
    }
}
