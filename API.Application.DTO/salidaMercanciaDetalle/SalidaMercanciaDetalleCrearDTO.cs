using System.ComponentModel.DataAnnotations;

namespace API.Application.DTO.salidaMercancia
{
    public class SalidaMercanciaDetalleCrearDTO
    {
        [Required(ErrorMessage = "{0} campo no debe de estar vacio.")]
        public int Entry { get; set; }

        // NoLinea no lo asigna el usuario: el backend numera las líneas 1..n al registrar.
        public string? CodArticulo { get; set; }
        public string? Descripcion { get; set; }
        public decimal? Cantidad { get; set; }

        // Costo por línea. Si viene nulo o <= 0 el servidor resuelve el costo vigente del
        // artículo (promedio o estándar según su método de valuación).
        public decimal? CostoUnitario { get; set; }
        public string? CodAlmacen { get; set; }
    }
}
