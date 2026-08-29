using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query;

namespace API.Service.WebApi.Tests.TestHelpers
{
    // Helper mínimo (patrón documentado por Microsoft para probar código EF Core con mocks) que
    // permite que un IQueryable<T> en memoria soporte los operadores async de EF Core
    // (FirstOrDefaultAsync, etc.). Sin esto, cualquier prueba que llegue a un código que use esos
    // operadores sobre un repo mockeado lanza en tiempo de ejecución.
    internal class TestAsyncEnumerable<T> : EnumerableQuery<T>, IAsyncEnumerable<T>, IQueryable<T>
    {
        public TestAsyncEnumerable(IEnumerable<T> enumerable) : base(enumerable) { }
        public TestAsyncEnumerable(Expression expression) : base(expression) { }

        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) =>
            new TestAsyncEnumerator<T>(this.AsEnumerable().GetEnumerator());

        IQueryProvider IQueryable.Provider => new TestAsyncQueryProvider<T>(this);
    }

    internal class TestAsyncEnumerator<T> : IAsyncEnumerator<T>
    {
        private readonly IEnumerator<T> _inner;
        public TestAsyncEnumerator(IEnumerator<T> inner) => _inner = inner;
        public T Current => _inner.Current;
        public ValueTask<bool> MoveNextAsync() => ValueTask.FromResult(_inner.MoveNext());
        public ValueTask DisposeAsync() { _inner.Dispose(); return ValueTask.CompletedTask; }
    }

    internal class TestAsyncQueryProvider<T> : IAsyncQueryProvider
    {
        private readonly IQueryProvider _inner;
        // NOTA: no usar "source.Provider" aquí -- TestAsyncEnumerable<T> sobreescribe IQueryable.Provider
        // devolviendo siempre "new TestAsyncQueryProvider<T>(this)", así que leer source.Provider
        // reentra a este mismo constructor indefinidamente (stack overflow). En su lugar se envuelve
        // la misma expresión en un EnumerableQuery<T> "plano" (sin la sobreescritura) para obtener el
        // proveedor real de LINQ-to-Objects.
        public TestAsyncQueryProvider(IQueryable<T> source) => _inner = ((IQueryable)new EnumerableQuery<T>(source.Expression)).Provider;

        public IQueryable CreateQuery(Expression expression) => new TestAsyncEnumerable<T>(expression);
        public IQueryable<TElement> CreateQuery<TElement>(Expression expression) => new TestAsyncEnumerable<TElement>(expression);
        public object? Execute(Expression expression) => _inner.Execute(expression);
        public TResult Execute<TResult>(Expression expression) => _inner.Execute<TResult>(expression);

        public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = default)
        {
            var expectedResultType = typeof(TResult).GetGenericArguments()[0];
            var executionResult = typeof(IQueryProvider)
                .GetMethod(nameof(IQueryProvider.Execute), 1, new[] { typeof(Expression) })!
                .MakeGenericMethod(expectedResultType)
                .Invoke(this, new object[] { expression });

            return (TResult)typeof(Task).GetMethod(nameof(Task.FromResult))!
                .MakeGenericMethod(expectedResultType)
                .Invoke(null, new[] { executionResult })!;
        }
    }

    internal static class QueryableAsyncExtensions
    {
        public static IQueryable<T> AsAsyncQueryable<T>(this IEnumerable<T> source) => new TestAsyncEnumerable<T>(source);
    }
}
