namespace API.Domain.Core.Inventario
{
    public class ArticuloNoExisteException : Exception
    {
        public ArticuloNoExisteException(string codArticulo)
            : base($"El artículo {codArticulo} no existe.") { }
    }

    public class AlmacenNoExisteException : Exception
    {
        public AlmacenNoExisteException(string codAlmacen)
            : base($"El almacén {codAlmacen} no existe.") { }
    }

    public class StockInsuficienteException : Exception
    {
        public StockInsuficienteException(string codArticulo, string codAlmacen, decimal disponible, decimal requerido)
            : base($"Stock insuficiente en el almacén {codAlmacen} para el artículo {codArticulo}: disponible {disponible}, requerido {requerido}.") { }
    }
}
