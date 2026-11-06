using API.Domain.Entity.Models;
using API.Domain.Interface;

namespace API.Domain.Core
{
    /// <summary>Construye la fila de AutorizacionCredito que los dominios insertan, en la misma transacción, junto al documento autorizado.</summary>
    public static class AutorizacionCreditoRegistro
    {
        /// <summary>Fila de AutorizacionCredito para el documento ya numerado (tipo 4/5/6 y entry).</summary>
        public static AutorizacionCredito Crear(int tipo, int entry, string codigoSn, string usuario, ResultadoCredito r)
        {
            if (r.Permitido || r.Posicion is null || r.Motivo is null)
                throw new InvalidOperationException("Solo un resultado de crédito rechazado y con posición se registra como autorización.");

            var p = r.Posicion;
            return new AutorizacionCredito
            {
                TipoDocumento = tipo,
                EntryDocumento = entry,
                CodigoSn = codigoSn,
                Usuario = usuario,
                Motivo = r.Motivo,
                Limite = p.Limite,
                Saldo = p.Saldo,
                Comprometido = p.Comprometido,
                Vencido = p.Vencido,
                Monto = r.Monto,
                Exceso = r.Exceso,
                Fecha = DateTime.Now
            };
        }
    }
}
