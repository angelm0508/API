using API.Domain.Core.Pos;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    /// <summary>Detección de interbloqueo (1205) y tiempo de espera de bloqueo (1222) de SQL Server sin referenciar el cliente SQL.</summary>
    public class ErrorSqlTransitorioTests
    {
        /// <summary>Imita a Microsoft.Data.SqlClient.SqlException: mismo nombre de tipo y propiedad pública Number.</summary>
        private sealed class SqlException : Exception
        {
            public SqlException(int number, string mensaje = "error sql") : base(mensaje) { Number = number; }
            public int Number { get; }
        }

        /// <summary>Otro tipo con Number, pero que no es SqlException: no debe tomarse en cuenta.</summary>
        private sealed class OtraExcepcion : Exception
        {
            public int Number => 1205;
        }

        [Theory]
        [InlineData(1205)]
        [InlineData(1222)]
        public void EsInterbloqueo_ExcepcionSqlDirecta_True(int numero) =>
            Assert.True(ErrorSqlTransitorio.EsInterbloqueo(new SqlException(numero)));

        [Theory]
        [InlineData(1205)]
        [InlineData(1222)]
        public void EsInterbloqueo_AnidadaEnDbUpdateException_True(int numero) =>
            Assert.True(ErrorSqlTransitorio.EsInterbloqueo(new DbUpdateException("No se pudo guardar.", new SqlException(numero))));

        [Fact]
        public void EsInterbloqueo_AnidadaEnVariosNiveles_True() =>
            Assert.True(ErrorSqlTransitorio.EsInterbloqueo(
                new Exception("a", new InvalidOperationException("b", new DbUpdateException("c", new SqlException(1205))))));

        [Fact]
        public void EsInterbloqueo_ErrorDeRestriccion547_False() =>
            Assert.False(ErrorSqlTransitorio.EsInterbloqueo(new DbUpdateException("FK", new SqlException(547))));

        [Fact]
        public void EsInterbloqueo_ExcepcionSinInterna_False() =>
            Assert.False(ErrorSqlTransitorio.EsInterbloqueo(new Exception("Stock insuficiente.")));

        [Fact]
        public void EsInterbloqueo_TipoQueNoSeLlamaSqlException_False() =>
            Assert.False(ErrorSqlTransitorio.EsInterbloqueo(new Exception("x", new OtraExcepcion())));

        [Fact]
        public void EsInterbloqueo_Nula_False() =>
            Assert.False(ErrorSqlTransitorio.EsInterbloqueo(null));

        [Fact]
        public void EsInterbloqueo_AgregadaConInterbloqueo_True() =>
            Assert.True(ErrorSqlTransitorio.EsInterbloqueo(new AggregateException(new Exception("x"), new SqlException(1205))));
    }
}
