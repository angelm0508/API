using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    public class NotaDebitoCompraDomain : INotaDebitoCompraDomain
    {
        // ---- Constantes propias del documento (en la nota de debito de proveedor cambian estas lineas) ----
        // CHECK constraint de la tabla: TipoObjeto='14'. Se fuerza siempre en el servidor.
        private const string TipoObjetoNotaDebitoCompra = "14";
        private const string TipoSocio = "P";
        private const int TipoDocBase = 13;
        private const string MsgSerieOtroObjeto = "La serie no corresponde a notas de débito de proveedor.";
        private const string MsgSocioTipo = "El socio de negocio debe ser un proveedor.";
        private const string MsgBaseRequerida = "Indique la factura de compra base.";
        private const string MsgBaseNoExiste = "La factura de compra base no existe.";
        private const string MsgBaseCancelada = "La factura de compra base está cancelada.";
        private const string MsgBaseOtroSocio = "La factura de compra base pertenece a otro socio de negocio.";
        private const string MsgFacturaNoExiste = "La factura de compra no existe.";
        private const string MsgFacturaCancelada = "La factura de compra está cancelada.";
        // ---- fin de constantes ----

        private const int SinBase = -1;
        private const int LongitudMaximaDescripcion = 200;

        private readonly IRepositorioGenerico<NotaDebitoCompra, int> _repoNota;
        private readonly IRepositorioGenerico<NotaDebitoCompraDetalle, (int Entry, int NoLinea)> _repoDetalle;
        private readonly IRepositorioGenerico<NumeracionDocumentoDet, int> _repoNumeracion;
        private readonly IRepositorioGenerico<SocioNegocio, string> _repoSocio;
        private readonly IRepositorioGenerico<Impuesto, string> _repoImpuesto;
        private readonly IRepositorioGenerico<Monedum, string> _repoMoneda;
        private readonly IRepositorioGenerico<FacturaCompra, int> _repoFacturaCompra;
        private readonly IRepositorioGenerico<FacturaCompraDetalle, (int Entry, int NoLinea)> _repoFacturaCompraDetalle;
        private readonly IEjecutorTransaccion _tx;

        public NotaDebitoCompraDomain(
            IRepositorioGenerico<NotaDebitoCompra, int> repoNota,
            IRepositorioGenerico<NotaDebitoCompraDetalle, (int Entry, int NoLinea)> repoDetalle,
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
        public async Task<int> InsertarAsync(NotaDebitoCompra obj, IEnumerable<NotaDebitoCompraDetalle> lineas)
        {
            var lineasList = lineas?.ToList() ?? new List<NotaDebitoCompraDetalle>();

            obj.TipoObjeto = TipoObjetoNotaDebitoCompra;
            obj.Cancelado = "N";                  // un documento recien registrado nunca nace cancelado
            obj.EstadoDoc = "A";
            obj.FechaCancelado = null;
            obj.FechaDoc ??= DateTime.Now;
            obj.FechaEmision ??= obj.FechaDoc;

            var serie = await ValidarSerieAsync(obj);
            await ValidarSocioYMonedaAsync(obj);
            await ValidarLineasAsync(lineasList);
            await ValidarBaseAsync(obj);

            AsignarNumero(obj, serie);

            return await _tx.EjecutarAsync(async () =>
            {
                await _repoNota.InsertarAsync(obj); // Save #1: asigna obj.Entry

                var noLinea = 1;
                foreach (var linea in lineasList)
                {
                    linea.Entry = obj.Entry;
                    linea.NoLinea = noLinea++;
                    linea.TipoObjeto = TipoObjetoNotaDebitoCompra;
                    await _repoDetalle.AgregarSinGuardarAsync(linea);
                }

                return obj.Entry;
            });
            // EjecutarAsync: Save #2 (lineas) + Commit. No hay asiento de inventario: la nota es solo monetaria.
        }

        public async Task<bool> ActualizarAsync(int id, NotaDebitoCompra obj)
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

        public async Task<NotaDebitoCompra> ObtenerAsync(int id)
        {
            return await _repoNota.ObtenerAsync(id);
        }

        public async Task<IQueryable<NotaDebitoCompra>> ObtenerTodoAsync()
        {
            return await _repoNota.ObtenerTodoAsync();
        }

        public async Task<IEnumerable<NotaDebitoCompraDetalle>> ObtenerLineasAsync(int id)
        {
            var queryable = await _repoDetalle.ObtenerTodoAsync();
            return await queryable.Where(d => d.Entry == id).OrderBy(d => d.NoLinea).ToListAsync();
        }

        public async Task<IEnumerable<FacturaCompra>> ObtenerFacturasCompraBaseAsync(string codigoSn)
        {
            var queryable = await _repoFacturaCompra.ObtenerTodoAsync();
            return await queryable
                .Where(f => f.CodigoSn == codigoSn && f.Cancelado != "S")
                .OrderByDescending(f => f.Entry)
                .ToListAsync();
        }

        public async Task<FacturaCompraBase> ObtenerFacturaCompraBaseAsync(int entry)
        {
            var factura = await _repoFacturaCompra.ObtenerAsync(entry)
                ?? throw new Exception(MsgFacturaNoExiste);

            if (factura.Cancelado == "S")
                throw new Exception(MsgFacturaCancelada);

            var queryable = await _repoFacturaCompraDetalle.ObtenerTodoAsync();
            var lineas = await queryable.Where(l => l.Entry == entry).OrderBy(l => l.NoLinea).ToListAsync();
            return new FacturaCompraBase(factura, lineas);
        }
        #endregion

        #region validaciones
        private async Task<NumeracionDocumentoDet> ValidarSerieAsync(NotaDebitoCompra obj)
        {
            var serie = await _repoNumeracion.ObtenerAsync(obj.Serie)
                ?? throw new Exception("La serie no existe.");

            if (serie.CodigoObj != TipoObjetoNotaDebitoCompra)
                throw new Exception(MsgSerieOtroObjeto);

            if (serie.Bloqueado == "S")
                throw new Exception("La serie está bloqueada y no se puede usar para registrar notas de débito.");

            if (serie.Manual == "S" && obj.NumDoc <= 0)
                throw new Exception("El número de documento es requerido para series manuales.");

            return serie;
        }

        // La numeracion automatica se asigna al final (despues de validar todo) para no avanzar el
        // consecutivo de una serie tracked si el documento resulta invalido.
        private static void AsignarNumero(NotaDebitoCompra obj, NumeracionDocumentoDet serie)
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

        private async Task ValidarSocioYMonedaAsync(NotaDebitoCompra obj)
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

        private async Task ValidarLineasAsync(IList<NotaDebitoCompraDetalle> lineas)
        {
            if (lineas.Count == 0)
                throw new Exception("El documento debe tener al menos una línea.");

            var impuestosVerificados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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

                if (impuestosVerificados.Add(linea.CodigoImpuesto))
                {
                    if (await _repoImpuesto.ObtenerAsync(linea.CodigoImpuesto) is null)
                        throw new Exception($"Línea {n}: el impuesto '{linea.CodigoImpuesto}' no existe.");
                }
            }
        }

        private async Task ValidarBaseAsync(NotaDebitoCompra obj)
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

            var factura = await _repoFacturaCompra.ObtenerAsync(obj.BaseEntry.Value)
                ?? throw new Exception(MsgBaseNoExiste);

            if (factura.Cancelado == "S")
                throw new Exception(MsgBaseCancelada);

            if (!string.Equals(factura.CodigoSn, obj.CodigoSn, StringComparison.OrdinalIgnoreCase))
                throw new Exception(MsgBaseOtroSocio);
        }
        #endregion
    }
}
