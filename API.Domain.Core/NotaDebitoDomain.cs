using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    /// <summary>
    /// Nota de débito de cliente. Se guarda en Factura/FacturaDetalle con DocSubType = 'ND' (modelo SAP B1).
    /// Por artículos (DocType 'I') mueve inventario como la factura; por concepto (DocType 'S') no.
    /// </summary>
    public class NotaDebitoDomain : INotaDebitoDomain
    {
        // ---- Constantes propias del documento (en la nota de debito de proveedor cambian estas lineas) ----
        // Factura.TipoObjeto / FacturaDetalle.TipoObjeto y TipoDoc del asiento de inventario.
        private const string TipoObjetoDocumento = "6";
        private const string SubTipoNota = "ND";                // Factura.DocSubType de la nota y SubTipoDoc de su serie
        private const string TipoSocio = "C";
        private const int TipoDocBase = 6;
        private const decimal SignoInventario = -1m;            // cliente: salida de stock (la de proveedor: +1, entrada)
        private const string MsgSerieOtroObjeto = "La serie no corresponde a notas de débito de cliente.";
        private const string MsgSocioTipo = "El socio de negocio debe ser un cliente.";
        private const string MsgBaseRequerida = "Indique la factura base.";
        private const string MsgBaseNoExiste = "La factura base no existe.";
        private const string MsgBaseEsNota = "La factura base no puede ser una nota de débito.";
        private const string MsgBaseCancelada = "La factura base está cancelada.";
        private const string MsgBaseOtroSocio = "La factura base pertenece a otro socio de negocio.";
        private const string MsgFacturaNoExiste = "La factura no existe.";
        private const string MsgFacturaCancelada = "La factura está cancelada.";
        // ---- fin de constantes ----

        private const string SubTipoFactura = "--";
        private const string ModoArticulos = "I";
        private const string ModoConcepto = "S";
        private const string MsgModoInvalido = "Indique el tipo de nota: por artículos ('I') o por concepto ('S').";
        private const int SinBase = -1;
        private const int LongitudMaximaDescripcion = 200;

        private readonly IRepositorioGenerico<Factura, int> _repoFactura;
        private readonly IRepositorioGenerico<FacturaDetalle, (int Entry, int NoLinea)> _repoDetalle;
        private readonly IRepositorioGenerico<NumeracionDocumentoDet, int> _repoNumeracion;
        private readonly IRepositorioGenerico<SocioNegocio, string> _repoSocio;
        private readonly IRepositorioGenerico<Impuesto, string> _repoImpuesto;
        private readonly IRepositorioGenerico<Monedum, string> _repoMoneda;
        private readonly IRepositorioGenerico<Articulo, string> _repoArticulo;
        private readonly IRepositorioGenerico<Almacen, string> _repoAlmacen;
        private readonly IEjecutorTransaccion _tx;
        private readonly IInventarioAsientoService _asiento;

        public NotaDebitoDomain(
            IRepositorioGenerico<Factura, int> repoFactura,
            IRepositorioGenerico<FacturaDetalle, (int Entry, int NoLinea)> repoDetalle,
            IRepositorioGenerico<NumeracionDocumentoDet, int> repoNumeracion,
            IRepositorioGenerico<SocioNegocio, string> repoSocio,
            IRepositorioGenerico<Impuesto, string> repoImpuesto,
            IRepositorioGenerico<Monedum, string> repoMoneda,
            IRepositorioGenerico<Articulo, string> repoArticulo,
            IRepositorioGenerico<Almacen, string> repoAlmacen,
            IEjecutorTransaccion tx,
            IInventarioAsientoService asiento)
        {
            _repoFactura = repoFactura;
            _repoDetalle = repoDetalle;
            _repoNumeracion = repoNumeracion;
            _repoSocio = repoSocio;
            _repoImpuesto = repoImpuesto;
            _repoMoneda = repoMoneda;
            _repoArticulo = repoArticulo;
            _repoAlmacen = repoAlmacen;
            _tx = tx;
            _asiento = asiento;
        }

        #region async methods
        public async Task<int> InsertarAsync(Factura obj, IEnumerable<FacturaDetalle> lineas)
        {
            var lineasList = lineas?.ToList() ?? new List<FacturaDetalle>();

            obj.DocSubType = SubTipoNota;
            obj.TipoObjeto = TipoObjetoDocumento;
            obj.Cancelado = "N";                  // un documento recien registrado nunca nace cancelado
            obj.EstadoDoc = "A";
            obj.Imprimido = "N";                  // nunca nace impreso, ignora lo que mande el cliente
            obj.FechaCancelado = null;
            obj.FechaDoc ??= DateTime.Now;
            obj.FechaEmision ??= obj.FechaDoc;

            if (obj.DocType != ModoArticulos && obj.DocType != ModoConcepto)
                throw new Exception(MsgModoInvalido);

            var serie = await ValidarSerieAsync(obj);
            await ValidarSocioYMonedaAsync(obj);
            await ValidarLineasAsync(obj, lineasList);
            await ValidarBaseAsync(obj);

            // EstadoInv siempre explicito: el DEFAULT 'A' de la base no debe decidir si la nota tiene asiento.
            obj.EstadoInv = obj.DocType == ModoArticulos ? "A" : "C";

            AsignarNumero(obj, serie);

            return await _tx.EjecutarAsync(async () =>
            {
                await _repoFactura.InsertarAsync(obj); // Save #1: asigna obj.Entry

                var noLinea = 1;
                foreach (var linea in lineasList)
                {
                    linea.Entry = obj.Entry;
                    linea.NoLinea = noLinea++;
                    linea.TipoObjeto = TipoObjetoDocumento;
                    await _repoDetalle.AgregarSinGuardarAsync(linea);
                }

                if (obj.DocType == ModoArticulos)
                {
                    var movimientos = lineasList
                        .Select(l => new MovimientoRequest(
                            TipoDoc: TipoObjetoDocumento,
                            DocEntry: obj.Entry,
                            DocLinea: l.NoLinea,
                            CodArticulo: l.CodArticulo!,
                            CodAlmacen: l.CodAlmacen!,
                            Cantidad: SignoInventario * l.Cantidad!.Value,
                            PrecioUnitario: l.Precio ?? 0m,
                            Fecha: obj.FechaDoc ?? DateTime.Now))
                        .ToList();

                    await _asiento.AsentarAsync(movimientos);
                }

                return obj.Entry;
            });
            // EjecutarAsync: Save #2 (lineas + inventario) + Commit.
        }

        public async Task<bool> ActualizarAsync(int id, Factura obj)
        {
            var existente = await ObtenerNotaAsync(id);
            if (existente is null)
                return false;

            if (existente.Cancelado == "S")
                throw new Exception("El documento está cancelado y no se puede modificar.");

            // Cancelacion: Cancelado pasa a 'S' (EstadoDoc no cambia, igual que en Factura).
            if (obj.Cancelado == "S")
            {
                return await _tx.EjecutarAsync(async () =>
                {
                    if (existente.DocType == ModoArticulos)
                        await _asiento.RevertirAsync(TipoObjetoDocumento, id);

                    existente.Cancelado = "S";
                    existente.EstadoInv = "C";
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
            var existente = await ObtenerNotaAsync(id);
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

                return await _repoFactura.EliminarAsync(id);
            });
        }

        public async Task<Factura?> ObtenerAsync(int id)
        {
            return await ObtenerNotaAsync(id);
        }

        public async Task<IQueryable<Factura>> ObtenerTodoAsync()
        {
            var filas = await _repoFactura.ObtenerTodoAsync();
            return filas.Where(f => f.DocSubType == SubTipoNota);
        }

        public async Task<IEnumerable<FacturaDetalle>> ObtenerLineasAsync(int id)
        {
            if (await ObtenerNotaAsync(id) is null)
                return Array.Empty<FacturaDetalle>();

            var queryable = await _repoDetalle.ObtenerTodoAsync();
            return await queryable.Where(d => d.Entry == id).OrderBy(d => d.NoLinea).ToListAsync();
        }

        public async Task<IEnumerable<Factura>> ObtenerFacturasBaseAsync(string codigoSn)
        {
            var queryable = await _repoFactura.ObtenerTodoAsync();
            return await queryable
                .Where(f => f.CodigoSn == codigoSn && f.Cancelado != "S" && f.DocSubType == SubTipoFactura)
                .OrderByDescending(f => f.Entry)
                .ToListAsync();
        }

        public async Task<FacturaBase> ObtenerFacturaBaseAsync(int entry)
        {
            var factura = await _repoFactura.ObtenerAsync(entry);
            if (factura is null || factura.DocSubType != SubTipoFactura)
                throw new Exception(MsgFacturaNoExiste);

            if (factura.Cancelado == "S")
                throw new Exception(MsgFacturaCancelada);

            var queryable = await _repoDetalle.ObtenerTodoAsync();
            var lineas = await queryable.Where(l => l.Entry == entry).OrderBy(l => l.NoLinea).ToListAsync();
            return new FacturaBase(factura, lineas);
        }
        #endregion

        #region validaciones
        // Una fila de Factura con otro DocSubType es una factura normal: su API es otra.
        private async Task<Factura?> ObtenerNotaAsync(int id)
        {
            var fila = await _repoFactura.ObtenerAsync(id);
            return fila is not null && fila.DocSubType == SubTipoNota ? fila : null;
        }

        private async Task<NumeracionDocumentoDet> ValidarSerieAsync(Factura obj)
        {
            var serie = await _repoNumeracion.ObtenerAsync(obj.Serie)
                ?? throw new Exception("La serie no existe.");

            if (serie.CodigoObj != TipoObjetoDocumento || serie.SubTipoDoc != SubTipoNota)
                throw new Exception(MsgSerieOtroObjeto);

            if (serie.Bloqueado == "S")
                throw new Exception("La serie está bloqueada y no se puede usar para registrar notas de débito.");

            if (serie.Manual == "S" && obj.NumDoc <= 0)
                throw new Exception("El número de documento es requerido para series manuales.");

            return serie;
        }

        // La numeracion automatica se asigna al final (despues de validar todo) para no avanzar el
        // consecutivo de una serie tracked si el documento resulta invalido.
        private static void AsignarNumero(Factura obj, NumeracionDocumentoDet serie)
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

        private async Task ValidarSocioYMonedaAsync(Factura obj)
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

        private async Task ValidarLineasAsync(Factura obj, IList<FacturaDetalle> lineas)
        {
            if (lineas.Count == 0)
                throw new Exception("El documento debe tener al menos una línea.");

            var porArticulos = obj.DocType == ModoArticulos;
            var impuestosVerificados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var articulosVerificados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var almacenesVerificados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < lineas.Count; i++)
            {
                var n = i + 1;
                var linea = lineas[i];

                var descripcion = (linea.Descripcion ?? string.Empty).Trim();
                if (!porArticulos && descripcion.Length == 0)
                    throw new Exception($"Línea {n}: la descripción es requerida.");
                if (descripcion.Length > LongitudMaximaDescripcion)
                    throw new Exception($"Línea {n}: la descripción no puede superar {LongitudMaximaDescripcion} caracteres.");
                linea.Descripcion = descripcion.Length == 0 ? null : descripcion;

                if (porArticulos)
                {
                    if (string.IsNullOrWhiteSpace(linea.CodArticulo))
                        throw new Exception($"Línea {n}: el artículo es requerido.");
                    if (string.IsNullOrWhiteSpace(linea.CodAlmacen))
                        throw new Exception($"Línea {n}: el almacén es requerido.");

                    if (articulosVerificados.Add(linea.CodArticulo) && await _repoArticulo.ObtenerAsync(linea.CodArticulo) is null)
                        throw new Exception($"Línea {n}: el artículo '{linea.CodArticulo}' no existe.");
                    if (almacenesVerificados.Add(linea.CodAlmacen) && await _repoAlmacen.ObtenerAsync(linea.CodAlmacen) is null)
                        throw new Exception($"Línea {n}: el almacén '{linea.CodAlmacen}' no existe.");
                }
                else if (!string.IsNullOrWhiteSpace(linea.CodArticulo) || !string.IsNullOrWhiteSpace(linea.CodAlmacen))
                {
                    throw new Exception($"Línea {n}: una nota por concepto no lleva artículo ni almacén.");
                }

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

                if (impuestosVerificados.Add(linea.CodigoImpuesto))
                {
                    if (await _repoImpuesto.ObtenerAsync(linea.CodigoImpuesto) is null)
                        throw new Exception($"Línea {n}: el impuesto '{linea.CodigoImpuesto}' no existe.");
                }
            }
        }

        private async Task ValidarBaseAsync(Factura obj)
        {
            var tipo = obj.BaseTipo ?? SinBase;

            if (tipo == SinBase)
            {
                obj.BaseTipo = SinBase;
                obj.BaseEntry = null;
                return;
            }

            if (tipo != TipoDocBase)
                throw new Exception("El tipo de documento base no es válido.");

            if (obj.BaseEntry is null)
                throw new Exception(MsgBaseRequerida);

            var factura = await _repoFactura.ObtenerAsync(obj.BaseEntry.Value)
                ?? throw new Exception(MsgBaseNoExiste);

            if (factura.DocSubType != SubTipoFactura)
                throw new Exception(MsgBaseEsNota);

            if (factura.Cancelado == "S")
                throw new Exception(MsgBaseCancelada);

            if (!string.Equals(factura.CodigoSn, obj.CodigoSn, StringComparison.OrdinalIgnoreCase))
                throw new Exception(MsgBaseOtroSocio);
        }
        #endregion
    }
}
