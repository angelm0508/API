using System.Data;
using System.Globalization;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    public class PagoRecibidoDomain : IPagoRecibidoDomain
    {
        // CHECK constraint de la tabla: TipoObjeto='24'. Se fuerza siempre en el servidor.
        private const string TipoObjetoPago = "24";
        private const string TipoSocioCliente = "C";
        private const string SubTipoNotaDebito = "ND";

        private readonly IRepositorioGenerico<PagoRecibido, int> _repoPago;
        private readonly IRepositorioGenerico<PagoRecibidoFactura, (int Entry, int NoLinea)> _repoPagoFactura;
        private readonly IRepositorioGenerico<PagoRecibidoMedio, (int Entry, int NoLinea)> _repoMedio;
        private readonly IRepositorioGenerico<FormaPago, string> _repoForma;
        private readonly IRepositorioGenerico<NumeracionDocumentoDet, int> _repoNumeracion;
        private readonly IRepositorioGenerico<SocioNegocio, string> _repoSocio;
        private readonly IRepositorioGenerico<Factura, int> _repoFactura;
        private readonly ISaldoFacturaService _saldos;
        private readonly IEjecutorTransaccion _tx;

        public PagoRecibidoDomain(
            IRepositorioGenerico<PagoRecibido, int> repoPago,
            IRepositorioGenerico<PagoRecibidoFactura, (int Entry, int NoLinea)> repoPagoFactura,
            IRepositorioGenerico<PagoRecibidoMedio, (int Entry, int NoLinea)> repoMedio,
            IRepositorioGenerico<FormaPago, string> repoForma,
            IRepositorioGenerico<NumeracionDocumentoDet, int> repoNumeracion,
            IRepositorioGenerico<SocioNegocio, string> repoSocio,
            IRepositorioGenerico<Factura, int> repoFactura,
            ISaldoFacturaService saldos,
            IEjecutorTransaccion tx)
        {
            _repoPago = repoPago;
            _repoPagoFactura = repoPagoFactura;
            _repoMedio = repoMedio;
            _repoForma = repoForma;
            _repoNumeracion = repoNumeracion;
            _repoSocio = repoSocio;
            _repoFactura = repoFactura;
            _saldos = saldos;
            _tx = tx;
        }

        #region async methods
        public async Task<int> InsertarAsync(PagoRecibido obj, IEnumerable<PagoRecibidoFactura> facturas, IEnumerable<PagoRecibidoMedio> medios)
        {
            var facturasList = facturas?.ToList() ?? new List<PagoRecibidoFactura>();
            var mediosList = medios?.ToList() ?? new List<PagoRecibidoMedio>();

            obj.TipoObjeto = TipoObjetoPago;
            obj.Cancelado = "N";                  // un documento recién registrado nunca nace cancelado
            obj.FechaCancelado = null;
            obj.FechaDoc ??= DateTime.Now;

            var serie = await ValidarSerieAsync(obj);
            await ValidarClienteAsync(obj);
            var documentos = await ValidarDocumentosAsync(obj.CodigoSn!, facturasList);
            await ValidarFormasDePagoAsync(mediosList);

            var totalAplicado = Redondear(facturasList.Sum(f => f.MontoAplicado));
            var totalMedios = Redondear(mediosList.Sum(m => m.Monto));
            if (totalAplicado != totalMedios)
                throw new Exception($"El total aplicado a los documentos ({Formato(totalAplicado)}) no coincide con el total de las formas de pago ({Formato(totalMedios)}).");
            obj.TotalPago = totalAplicado;

            // La numeración automática se asigna al final (después de validar todo) para no avanzar el
            // consecutivo de una serie rastreada si el documento resulta inválido.
            AsignarNumero(obj, serie);

            // Serializable: el chequeo del saldo y el insert son una unidad; dos pagos simultáneos sobre la misma
            // factura no pueden exceder el saldo entre los dos.
            return await _tx.EjecutarAsync(async () =>
            {
                await ValidarSaldosAsync(facturasList, documentos);

                await _repoPago.InsertarAsync(obj); // Save #1: asigna obj.Entry

                var noLinea = 1;
                foreach (var linea in facturasList)
                {
                    linea.Entry = obj.Entry;
                    linea.NoLinea = noLinea++;
                    await _repoPagoFactura.AgregarSinGuardarAsync(linea);
                }

                noLinea = 1;
                foreach (var medio in mediosList)
                {
                    medio.Entry = obj.Entry;
                    medio.NoLinea = noLinea++;
                    await _repoMedio.AgregarSinGuardarAsync(medio);
                }

                return obj.Entry;
            }, IsolationLevel.Serializable);
            // EjecutarAsync: Save #2 (líneas) + Commit. No hay asiento de inventario ni contable.
        }

        public async Task<bool> CancelarAsync(int id)
        {
            var existente = await _repoPago.ObtenerAsync(id);
            if (existente is null)
                return false;

            if (existente.Cancelado == "S")
                throw new Exception("El pago ya está cancelado.");

            return await _tx.EjecutarAsync(async () =>
            {
                await Task.CompletedTask;
                existente.Cancelado = "S";
                existente.FechaCancelado = DateTime.Now;
                return true;
            });
        }

        public async Task<PagoRecibido?> ObtenerAsync(int id)
        {
            return await _repoPago.ObtenerAsync(id);
        }

        public async Task<IQueryable<PagoRecibido>> ObtenerTodoAsync()
        {
            return await _repoPago.ObtenerTodoAsync();
        }

        public async Task<IEnumerable<PagoRecibidoFactura>> ObtenerFacturasAsync(int id)
        {
            var lineas = await _repoPagoFactura.ObtenerTodoAsync();
            return await lineas.Where(l => l.Entry == id).OrderBy(l => l.NoLinea).ToListAsync();
        }

        public async Task<IEnumerable<PagoRecibidoMedio>> ObtenerMediosAsync(int id)
        {
            var medios = await _repoMedio.ObtenerTodoAsync();
            return await medios.Where(m => m.Entry == id).OrderBy(m => m.NoLinea).ToListAsync();
        }

        public async Task<IReadOnlyList<DocumentoPorCobrar>> ObtenerDocumentosPorCobrarAsync(string codigoSn)
        {
            var queryable = await _repoFactura.ObtenerTodoAsync();
            var documentos = await queryable
                .Where(f => f.CodigoSn == codigoSn && f.Cancelado != "S")
                .OrderBy(f => f.FechaDoc).ThenBy(f => f.Entry)
                .ToListAsync();
            if (documentos.Count == 0)
                return new List<DocumentoPorCobrar>();

            var saldos = await _saldos.ObtenerAsync(documentos.Select(d => d.Entry));
            return documentos
                .Where(d => saldos.TryGetValue(d.Entry, out var s) && s.Saldo > 0m)
                .Select(d => new DocumentoPorCobrar(d, saldos[d.Entry]))
                .ToList();
        }
        #endregion

        #region validaciones y calculos
        private static decimal Redondear(decimal valor) => Math.Round(valor, 2, MidpointRounding.AwayFromZero);

        private static string Formato(decimal valor) => valor.ToString("0.00", CultureInfo.InvariantCulture);

        private static string Etiqueta(Factura f) => (f.DocSubType == SubTipoNotaDebito ? "Nota de débito " : "Factura ") + f.NumDoc;

        private async Task<NumeracionDocumentoDet> ValidarSerieAsync(PagoRecibido obj)
        {
            var serie = await _repoNumeracion.ObtenerAsync(obj.Serie)
                ?? throw new Exception("La serie no existe.");

            if (serie.CodigoObj != TipoObjetoPago)
                throw new Exception("La serie no corresponde a pagos recibidos.");

            if (serie.Bloqueado == "S")
                throw new Exception("La serie está bloqueada y no se puede usar para registrar pagos recibidos.");

            if (serie.Manual == "S" && obj.NumDoc <= 0)
                throw new Exception("El número de documento es requerido para series manuales.");

            return serie;
        }

        // Un cliente dado de baja con deuda debe poder pagarla: no se exige que esté activo.
        private async Task ValidarClienteAsync(PagoRecibido obj)
        {
            if (string.IsNullOrWhiteSpace(obj.CodigoSn))
                throw new Exception("El socio de negocio es requerido.");

            var socio = await _repoSocio.ObtenerAsync(obj.CodigoSn)
                ?? throw new Exception("El socio de negocio no existe.");

            if (socio.TipoSn != TipoSocioCliente)
                throw new Exception("El socio de negocio debe ser un cliente.");

            obj.NombreSn = socio.Nombre;
        }

        private async Task<Dictionary<int, Factura>> ValidarDocumentosAsync(string codigoSn, List<PagoRecibidoFactura> lineas)
        {
            if (lineas.Count == 0)
                throw new Exception("Indique al menos un documento a pagar.");

            var repetido = lineas.GroupBy(l => l.FacturaEntry).FirstOrDefault(g => g.Count() > 1);
            if (repetido != null)
                throw new Exception($"El documento {repetido.Key} está repetido en el pago.");

            var documentos = new Dictionary<int, Factura>();
            foreach (var linea in lineas)
            {
                var factura = await _repoFactura.ObtenerAsync(linea.FacturaEntry)
                    ?? throw new Exception($"El documento {linea.FacturaEntry} no existe.");
                var etiqueta = Etiqueta(factura);

                if (!string.Equals(factura.CodigoSn, codigoSn, StringComparison.OrdinalIgnoreCase))
                    throw new Exception($"{etiqueta} pertenece a otro socio de negocio.");

                if (factura.Cancelado == "S")
                    throw new Exception($"{etiqueta} está cancelada.");

                if (linea.MontoAplicado <= 0m)
                    throw new Exception($"El monto a pagar de {etiqueta} debe ser mayor que 0.");

                if (Redondear(linea.MontoAplicado) != linea.MontoAplicado)
                    throw new Exception($"El monto a pagar de {etiqueta} no puede tener más de 2 decimales.");

                documentos[linea.FacturaEntry] = factura;
            }
            return documentos;
        }

        private async Task ValidarFormasDePagoAsync(List<PagoRecibidoMedio> medios)
        {
            // Reglas y mensajes compartidos con pagos efectuados (ValidadorMediosPago).
            await ValidadorMediosPago.ValidarAsync(medios, codigo => _repoForma.ObtenerAsync(codigo));
        }

        /// <summary>Corre dentro de la transaccion Serializable del insert.</summary>
        private async Task ValidarSaldosAsync(List<PagoRecibidoFactura> lineas, Dictionary<int, Factura> documentos)
        {
            // Relectura del estado dentro de la tx (proyección, sin la caché de FindAsync): un documento cancelado entre
            // ValidarDocumentosAsync y este punto no puede recibir el pago. La lectura Serializable bloquea la fila hasta el commit.
            var ids = lineas.Select(l => l.FacturaEntry).Distinct().ToList();
            var estados = await (await _repoFactura.ObtenerTodoAsync())
                .Where(f => ids.Contains(f.Entry))
                .Select(f => new { f.Entry, f.Cancelado })
                .ToListAsync();
            var cancelado = estados.FirstOrDefault(e => e.Cancelado == "S");
            if (cancelado != null)
                throw new Exception($"{Etiqueta(documentos[cancelado.Entry])} está cancelada.");

            var saldos = await _saldos.ObtenerAsync(lineas.Select(l => l.FacturaEntry));
            foreach (var linea in lineas)
            {
                var disponible = saldos.TryGetValue(linea.FacturaEntry, out var s) ? s.Saldo : 0m;
                if (linea.MontoAplicado > disponible)
                    throw new Exception($"El monto a pagar de {Etiqueta(documentos[linea.FacturaEntry])} ({Formato(linea.MontoAplicado)}) excede su saldo ({Formato(disponible)}).");
            }
        }

        private static void AsignarNumero(PagoRecibido obj, NumeracionDocumentoDet serie)
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
            serie.SigNumero = serie.SigNumero.Value + 1; // rastreada por el mismo contexto; se persiste en el Save de la transacción
        }
        #endregion
    }
}
