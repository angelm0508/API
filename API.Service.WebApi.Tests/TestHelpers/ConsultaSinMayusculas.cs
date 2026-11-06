using System.Collections;
using System.Linq.Expressions;
using System.Reflection;

namespace API.Service.WebApi.Tests.TestHelpers
{
    // La base de datos compara cadenas sin distinguir mayúsculas; LINQ a objetos sí las distingue.
    // Este envoltorio reescribe los Contains sobre colecciones de cadenas para que comparen con
    // OrdinalIgnoreCase y así las pruebas en memoria se comporten como SQL Server en los filtros por código.
    internal class ConsultaSinMayusculas<T> : IOrderedQueryable<T>, IAsyncEnumerable<T>
    {
        private readonly IQueryable<T> _interna;
        public ConsultaSinMayusculas(IQueryable<T> interna) => _interna = interna;

        public Type ElementType => _interna.ElementType;
        public Expression Expression => _interna.Expression;
        public IQueryProvider Provider => new ProveedorSinMayusculas(_interna.Provider);
        public IEnumerator<T> GetEnumerator() => _interna.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) =>
            ((IAsyncEnumerable<T>)_interna).GetAsyncEnumerator(cancellationToken);
    }

    internal class ProveedorSinMayusculas : IQueryProvider
    {
        private readonly IQueryProvider _interno;
        public ProveedorSinMayusculas(IQueryProvider interno) => _interno = interno;

        public IQueryable CreateQuery(Expression expression) => throw new NotSupportedException();

        public IQueryable<TElement> CreateQuery<TElement>(Expression expression) =>
            new ConsultaSinMayusculas<TElement>(_interno.CreateQuery<TElement>(new Reescritor().Visit(expression)));

        public object? Execute(Expression expression) => _interno.Execute(new Reescritor().Visit(expression));
        public TResult Execute<TResult>(Expression expression) => _interno.Execute<TResult>(new Reescritor().Visit(expression));

        private sealed class Reescritor : ExpressionVisitor
        {
            private static readonly MethodInfo ContainsConComparador = typeof(Enumerable).GetMethods()
                .Single(m => m.Name == nameof(Enumerable.Contains) && m.GetParameters().Length == 3)
                .MakeGenericMethod(typeof(string));

            protected override Expression VisitMethodCall(MethodCallExpression node)
            {
                var comparador = Expression.Constant(StringComparer.OrdinalIgnoreCase, typeof(IEqualityComparer<string>));
                // list.Contains(x) sobre List<string> / string[] como instancia.
                if (node.Method.Name == nameof(List<string>.Contains) && node.Object != null
                    && typeof(IEnumerable<string>).IsAssignableFrom(node.Object.Type) && node.Arguments.Count == 1)
                    return Expression.Call(ContainsConComparador, Expression.Convert(Visit(node.Object), typeof(IEnumerable<string>)), Visit(node.Arguments[0]), comparador);
                // Enumerable.Contains(list, x).
                if (node.Method.IsGenericMethod && node.Method.GetGenericMethodDefinition() == typeof(Enumerable).GetMethods()
                        .Single(m => m.Name == nameof(Enumerable.Contains) && m.GetParameters().Length == 2)
                    && node.Method.GetGenericArguments()[0] == typeof(string))
                    return Expression.Call(ContainsConComparador, Visit(node.Arguments[0]), Visit(node.Arguments[1]), comparador);
                return base.VisitMethodCall(node);
            }
        }
    }

    internal static class ConsultaSinMayusculasExtensions
    {
        public static IQueryable<T> AsAsyncQueryableSinMayusculas<T>(this IEnumerable<T> source) =>
            new ConsultaSinMayusculas<T>(source.AsAsyncQueryable());
    }
}
