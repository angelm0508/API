namespace API.Application.DTO.documentoBase
{
    public class DocumentoBaseBorradorDTO
    {
        public int OrigenTipo { get; set; }
        public int OrigenEntry { get; set; }
        public int OrigenNumDoc { get; set; }
        public DocumentoBaseCabeceraDTO Cabecera { get; set; } = new();
        public List<DocumentoBaseLineaDTO> Lineas { get; set; } = new();
    }

    public class DocumentoBaseCabeceraDTO
    {
        public string? CodigoSn { get; set; }
        public string? NombreSn { get; set; }
        public string? Direccion { get; set; }
        public string? MonedaDoc { get; set; }
        public decimal? PrctjeDesc { get; set; }
        public decimal? PrctjeImpuesto { get; set; }
        public string? Comentario { get; set; }
    }

    public class DocumentoBaseLineaDTO
    {
        public int BaseTipo { get; set; }
        public int BaseEntry { get; set; }
        public int BaseLinea { get; set; }
        public string? CodArticulo { get; set; }
        public string? Descripcion { get; set; }
        public decimal CantidadOrigen { get; set; }
        public decimal CantidadAbierta { get; set; }
        public decimal? Precio { get; set; }
        public decimal? PrecioBruto { get; set; }
        public decimal? PrctjeDesc { get; set; }
        public string? CodigoImpuesto { get; set; }
        public decimal? Impuesto { get; set; }
        public string? CodAlmacen { get; set; }
    }
}
