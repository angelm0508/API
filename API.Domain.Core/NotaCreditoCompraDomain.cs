using System.Data;
using System.Globalization;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    public class NotaCreditoCompraDomain : INotaCreditoCompraDomain
    {
        // ---- Constantes propias del documento (en la nota de credito de proveedor cambian estas lineas) ----
        // CHECK constraint de la tabla: TipoObjeto='15'. Se fuerza siempre en el servidor.
        private const string TipoObjetoNotaCreditoCompra = "15";
        private const string TipoSocio = "P";
        private const int TipoDocBase = 13;
        private const string MsgSerieOtroObjeto = "La serie no corresponde a notas de crédito de proveedor.";
        private const string MsgSocioTipo = "El socio de negocio debe ser un proveedor.";
        private const string MsgBaseRequerida = "Indique la factura de compra base.";
        private const string MsgBaseNoExiste = "La factura de compra base no existe.";
        private const string MsgBaseCancelada = "La factura de compra base está cancelada.";
        private const string MsgBaseOtroSocio = "La factura de compra base pertenece a otro socio de negocio.";
        private const string MsgBaseMoneda = "La moneda de la nota de crédito debe ser la misma de la factura de compra base.";
        private const string MsgExceso = "El total acreditado ({0}) excede el total de la factura de compra base ({1}).";
        private const string MsgFacturaNoExiste = "La factura de compra no existe.";
        private const string MsgFacturaCancelada = "La factura de compra está cancelada.";
        // ---- fin de constantes ----

        private const int SinBase = -1;
        private const int LongitudMaximaDescripcion = 200;

        private readonly IRepositorioGenerico<NotaCreditoCompra, int> _repoNota;
        private readonly IRepositorioGenerico<NotaCreditoCompraDetalle, (int Entry, int NoLinea)> _repoDetalle;
        private readonly IRepositorioGenerico<NumeracionDocumentoDet, int> _repoNumeracion;
        private readonly IRepositorioGenerico<SocioNegocio, string> _repoSocio;
        private readonly IRepositorioGenerico<Impuesto, string> _repoImpuesto;
        private readonly IRepositorioGenerico<Monedum, string> _repoMoneda;
        private readonly IRepositorioGenerico<FacturaCompra, int> _repoFacturaCompra;
        private readonly IRepositorioGenerico<FacturaCompraDetalle, (int Entry, int NoLinea)> _repoFacturaCompraDetalle;
        private readonly IEjecutorTransaccion _tx;

        public NotaCreditoCompraDomain(
            IRepositorioGenerico<NotaCreditoCompra, int> repoNota,
            IRepositorioGenerico<NotaCreditoCompraDetalle, (int Entry, int NoLinea)> repoDetalle,
            IRepositorioGenerico<NumeracionDocumentoDet, int> repoNumeracion,
            IRepositorioGenerico<SocioNegocio, string> repoSocio,
            IRepositorioGenerico<Impuesto, string> repoImpuesto,
            IRepositorioGenerico<Monedum, string> repoMoneda,
            IRepositorioGenerico<FacturaCompra, int> repoFacturaCompra,
            IRepositorioGenerico<FacturaCompraDetalle, (int Entry, int NoLinea)> repoFacturaCompraDetalle,
            IEjecutorTransaccion tx)
        {
            _repoNota = repoNota;
            _repoDetalle = repoDetalle;
            _repoNumeracion = repoNumeracion;
            _repoSocio = repoSocio;
            _repoImpuesto = repoImpuesto;
            _repoMoneda = repoMoneda;
            _repoFacturaCompra = repoFacturaCompra;
            _repoFacturaCompraDetalle = repoFacturaCompraDetalle;
            _tx = tx;
        }

        #region async methods
        public async Task<int> InsertarAsync(NotaCreditoCompra obj, IEnumerable<NotaCreditoCompraDetalle> lineas)
        {
            var lineasList = lineas?.ToList() ?? new List<NotaCreditoCompraDetalle>();

            obj.TipoObjeto = TipoObjetoNotaCreditoCompra;
            obj.Cancelado = "N";                  // un documento recien registrado nunca nace cancelado
            obj.EstadoDoc = "A";
            obj.Imprimido = "N";
            obj.FechaCancelado = null;
            obj.FechaDoc ??= DateTime.Now;
            obj.FechaEmision ??= obj.FechaDoc;

            var serie = await ValidarSerieAsync(obj);
            await ValidarSocioYMonedaAsync(obj);
            var tasas = await ValidarLineasAsync(lineasList);
            RecalcularTotales(obj, lineasList, tasas);
            var facturaBase = await ValidarBaseAsync(obj);

            AsignarNumero(obj, serie);

            // Serializable: el chequeo del limite acreditable y el insert son una unidad; dos notas
            // simultaneas sobre la misma factura no pueden exceder el total entre las dos.
            return await _tx.EjecutarAsync(async () =>
            {
                if (facturaBase is not null)
                    await ValidarLimiteAcreditableAsync(facturaBase, obj);

                await _repoNota.InsertarAsync(obj); // Save #1: asigna obj.Entry

                var noLinea = 1;
                foreach (var linea in lineasList)
                {
                    linea.Entry = obj.Entry;
                    linea.NoLinea = noLinea++;
                    linea.TipoObjeto = TipoObjetoNotaCreditoCompra;
                    await _repoDetalle.AgregarSinGuardarAsync(linea);
                }

                return obj.Entry;
            }, IsolationLevel.Serializable);
            // EjecutarAsync: Save #2 (lineas) + Commit. No hay asiento de inventario: la nota es solo monetaria.
        }

        public async Task<bool> ActualizarAsync(int id, NotaCreditoCompra obj)
        {
            var existente = await _repoNota.ObtenerAsync(id);
            if (existente is null)
                return false;

            if (existente.Cancelado == "S")
                throw new Exception("El documento está cancelado y no se puede modificar.");

            // Cancelacion: Cancelado pasa a 'S' (EstadoDoc no cambia, igual que en FacturaCompra).
            if (obj.Cancelado == "S")
            {
                return await _tx.EjecutarAsync(async () =>
                {
                    await Task.CompletedTask;
                    existente.Cancelado = "S";
                    existente.FechaCancelado = DateTime.Now;
                    // Solo se toca el comentario si el cliente lo envio: el boton "Cancelar documento"
                    // del Web manda unicamente { Cancelado: 'S' } y no debe borrar la nota existente.
                    if (obj.Comentario != null)
                        existente.Comentario = obj.Comentario;
                    return true;
                });
            }

            // Edicion inocua: solo el comentario (replace-semantics: enviar vacio lo borra).
            return await _tx.EjecutarAsync(async () =>
            {
                await Task.CompletedTask;
                existente.Comentario = obj.Comentario;
                return true;
            });
        }

        public async Task<bool> EliminarAsync(int id)
        {
            var existente = await _repoNota.ObtenerAsync(id);
            if (existente is null)
                return false;

            if (existente.Cancelado != "S")
                throw new Exception("Cancele el documento (Cancelado='S') antes de eliminarlo.");

            return await _tx.EjecutarAsync(async () =>
            {
                var detalles = await _repoDetalle.ObtenerTodoAsync();
                var lineas = await detalles.Where(d => d.Entry == id).ToListAsync();
                foreach (var linea in lineas)
                    await _repoDetalle.EliminarAsync((linea.Entry, linea.NoLinea));

                return await _repoNota.EliminarAsync(id);
            });
        }

        public async Task<NotaCreditoCompra> ObtenerAsync(int id)
        {
            return await _repoNota.ObtenerAsync(id);
        }

        public async Task<IQueryable<NotaCreditoCompra>> ObtenerTodoAsync()
        {
            return await _repoNota.ObtenerTodoAsync();
        }

        public async Task<IEnumerable<NotaCreditoCompraDetalle>> ObtenerLineasAsync(int id)
        {
            var queryable = await _repoDetalle.ObtenerTodoAsync();
            return await queryable.Where(d => d.Entry == id).OrderBy(d => d.NoLinea).ToListAsync();
        }

        public async Task<IEnumerable<FacturaCompraAcreditable>> ObtenerFacturasCompraBaseAsync(string codigoSn)
        {
            var queryable = await _repoFacturaCompra.ObtenerTodoAsync();
            var facturas = await queryable
                .Where(f => f.CodigoSn == codigoSn && f.Cancelado != "S")
                .OrderByDescending(f => f.Entry)
                .ToListAsync();

            if (facturas.Count == 0)
                return new List<FacturaCompraAcreditable>();

            // Una sola consulta de las notas no canceladas sobre esas facturas; se suma en memoria.
            var entries = facturas.Select(f => f.Entry).ToList();
            var notas = await (await _repoNota.ObtenerTodoAsync())
                .Where(n => n.BaseTipo == TipoDocBase && n.Cancelado != "S" && n.BaseEntry != null && entries.Contains(n.BaseEntry.Value))
                .Select(n => new { Base = n.BaseEntry, Total = n.TotalDoc ?? 0m })
                .ToListAsync();
            var acreditadoPorFactura = notas
                .GroupBy(n => n.Base!.Value)
                .ToDictionary(g => g.Key, g => Redondear(g.Sum(n => n.Total)));

            return facturas
                .Select(f => new FacturaCompraAcreditable(f, acreditadoPorFactura.TryGetValue(f.Entry, out var a) ? a : 0m))
                .ToList();
        }

        public async Task<FacturaCompraBaseCredito> ObtenerFacturaCompraBaseAsync(int entry)
        {
            var factura = await _repoFacturaCompra.ObtenerAsync(entry)
                ?? throw new Exception(MsgFacturaNoExiste);

            if (factura.Cancelado == "S")
                throw new Exception(MsgFacturaCancelada);

            var queryable = await _repoFacturaCompraDetalle.ObtenerTodoAsync();
            var lineas = await queryable.Where(l => l.Entry == entry).OrderBy(l => l.NoLinea).ToListAsync();
            var acreditado = await ObtenerAcreditadoAsync(entry);
            return new FacturaCompraBaseCredito(factura, lineas, acreditado);
        }
        #endregion

        #region validaciones y calculos
        private static decimal Redondear(decimal valor) => Math.Round(valor, 2, MidpointRounding.AwayFromZero);

        private async Task<NumeracionDocumentoDet> ValidarSerieAsync(NotaCreditoCompra obj)
        {
            var serie = await _repoNumeracion.ObtenerAsync(obj.Serie)
                ?? throw new Exception("La serie no existe.");

            if (serie.CodigoObj != TipoObjetoNotaCreditoCompra)
                throw new Exception(MsgSerieOtroObjeto);

            if (serie.Bloqueado == "S")
                throw new Exception("La serie está bloqueada y no se puede usar para registrar notas de crédito.");

            if (serie.Manual == "S" && obj.NumDoc <= 0)
                throw new Exception("El número de documento es requerido para series manuales.");

            return serie;
        }

        // La numeracion automatica se asigna al final (despues de validar todo) para no avanzar el
        // consecutivo de una serie tracked si el documento resulta invalido.
        private static void AsignarNumero(NotaCreditoCompra obj, NumeracionDocumentoDet serie)
        {
            if (serie.Manual == "S")
            {
                obj.NumManual = "S";
                return;
            }

            if (serie.SigNumero == null)
                throw new Exception("La serie no tiene configurado el número siguiente.");
            if (serie.FinNumero.HasValue && serie.SigNumero.Value > serie.FinNumero.Value)
                throw new Exception("Se agotó la numeración disponible en esta serie.");

            obj.NumManual = "N";
            obj.NumDoc = serie.SigNumero.Value;
            serie.SigNumero = serie.SigNumero.Value + 1; // rastreada por el mismo contexto; se persiste en el Save de la transaccion
        }

        private async Task ValidarSocioYMonedaAsync(NotaCreditoCompra obj)
        {
            if (string.IsNullOrWhiteSpace(obj.CodigoSn))
                throw new Exception("El socio de negocio es requerido.");

            var socio = await _repoSocio.ObtenerAsync(obj.CodigoSn)
                ?? throw new Exception("El socio de negocio no existe.");

            if (socio.TipoSn != TipoSocio)
                throw new Exception(MsgSocioTipo);

            if (socio.Activo == "N")
                throw new Exception("El socio de negocio está inactivo.");

            if (string.IsNullOrWhiteSpace(obj.MonedaDoc))
                throw new Exception("La moneda es requerida.");

            if (await _repoMoneda.ObtenerAsync(obj.MonedaDoc) is null)
                throw new Exception("La moneda no existe.");
        }

        /// <summary>Valida las lineas y devuelve la tasa (%) de cada codigo de impuesto usado.</summary>
        private async Task<Dictionary<string, decimal>> ValidarLineasAsync(IList<NotaCreditoCompraDetalle> lineas)
        {
            if (lineas.Count == 0)
                throw new Exception("El documento debe tener al menos una línea.");

            var tasas = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < lineas.Count; i++)
            {
                var n = i + 1;
                var linea = lineas[i];

                var descripcion = (linea.Descripcion ?? string.Empty).Trim();
                if (descripcion.Length == 0)
                    throw new Exception($"Línea {n}: la descripción es requerida.");
                if (descripcion.Length > LongitudMaximaDescripcion)
                    throw new Exception($"Línea {n}: la descripción no puede superar {LongitudMaximaDescripcion} caracteres.");
                linea.Descripcion = descripcion;

                if ((linea.Cantidad ?? 0m) <= 0m)
                    throw new Exception($"Línea {n}: la cantidad debe ser mayor que 0.");

                if (linea.Precio is null)
                    throw new Exception($"Línea {n}: el precio es requerido.");
                if (linea.Precio < 0m)
                    throw new Exception($"Línea {n}: el precio no puede ser negativo.");

                var descuento = linea.PrctjeDesc ?? 0m;
                if (descuento < 0m || descuento > 100m)
                    throw new Exception($"Línea {n}: el descuento debe estar entre 0 y 100.");
                linea.PrctjeDesc = descuento;

                if (string.IsNullOrWhiteSpace(linea.CodigoImpuesto))
                    throw new Exception($"Línea {n}: el impuesto es requerido.");

                if (!tasas.ContainsKey(linea.CodigoImpuesto))
                {
                    var impuesto = await _repoImpuesto.ObtenerAsync(linea.CodigoImpuesto)
                        ?? throw new Exception($"Línea {n}: el impuesto '{linea.CodigoImpuesto}' no existe.");
                    tasas[linea.CodigoImpuesto] = impuesto.Tasa ?? 0m;
                }
            }

            return tasas;
        }

        /// <summary>
        /// El servidor es la unica fuente de los montos: recalcula impuesto y total de cada linea y los
        /// totales del encabezado; lo que mande el cliente se ignora.
        /// </summary>
        private static void RecalcularTotales(NotaCreditoCompra obj, IList<NotaCreditoCompraDetalle> lineas, IReadOnlyDictionary<string, decimal> tasas)
        {
            decimal totalBruto = 0m, totalDesc = 0m, totalImp = 0m, totalDoc = 0m;

            foreach (var linea in lineas)
            {
                var bruto = Redondear(linea.Cantidad!.Value * linea.Precio!.Value);
                var descuento = Redondear(bruto * linea.PrctjeDesc!.Value / 100m);
                var subtotal = bruto - descuento;
                var impuesto = Redondear(subtotal * tasas[linea.CodigoImpuesto] / 100m);

                linea.PrecioBruto = bruto;
                linea.Impuesto = impuesto;
                linea.TotalLinea = subtotal + impuesto;

                totalBruto += bruto;
                totalDesc += descuento;
                totalImp += impuesto;
                totalDoc += linea.TotalLinea.Value;
            }

            obj.TotalBruto = totalBruto;
            obj.TotalDesc = totalDesc;
            obj.TotalImp = totalImp;
            obj.TotalDoc = totalDoc;
        }

        /// <summary>Valida la factura base (si hay) y la devuelve; sin base devuelve null.</summary>
        private async Task<FacturaCompra?> ValidarBaseAsync(NotaCreditoCompra obj)
        {
            var tipo = obj.BaseTipo ?? SinBase;

            if (tipo == SinBase)
            {
                obj.BaseTipo = SinBase;
                obj.BaseEntry = null;
                return null;
            }

            if (tipo != TipoDocBase)
                throw new Exception("El tipo de documento base no es válido.");

            if (obj.BaseEntry is null)
                throw new Exception(MsgBaseRequerida);

            var factura = await _repoFacturaCompra.ObtenerAsync(obj.BaseEntry.Value)
                ?? throw new Exception(MsgBaseNoExiste);

            if (factura.Cancelado == "S")
                throw new Exception(MsgBaseCancelada);

            if (!string.Equals(factura.CodigoSn, obj.CodigoSn, StringComparison.OrdinalIgnoreCase))
                throw new Exception(MsgBaseOtroSocio);

            if (!string.Equals(factura.MonedaDoc, obj.MonedaDoc, StringComparison.OrdinalIgnoreCase))
                throw new Exception(MsgBaseMoneda);

            return factura;
        }

        private async Task<decimal> ObtenerAcreditadoAsync(int facturaEntry)
        {
            var queryable = await _repoNota.ObtenerTodoAsync();
            var totales = await queryable
                .Where(n => n.BaseTipo == TipoDocBase && n.BaseEntry == facturaEntry && n.Cancelado != "S")
                .Select(n => n.TotalDoc ?? 0m)
                .ToListAsync();
            return Redondear(totales.Sum());
        }

        /// <summary>Corre dentro de la transaccion Serializable del insert.</summary>
        private async Task ValidarLimiteAcreditableAsync(FacturaCompra factura, NotaCreditoCompra obj)
        {
            var acreditado = await ObtenerAcreditadoAsync(factura.Entry);
            var totalConEstaNota = Redondear(acreditado + (obj.TotalDoc ?? 0m));
            var totalFactura = Redondear(factura.TotalDoc ?? 0m);

            if (totalConEstaNota > totalFactura)
                throw new Exception(string.Format(CultureInfo.InvariantCulture, MsgExceso,
                    totalConEstaNota.ToString("0.00", CultureInfo.InvariantCulture),
                    totalFactura.ToString("0.00", CultureInfo.InvariantCulture)));
        }
        #endregion
    }
}
