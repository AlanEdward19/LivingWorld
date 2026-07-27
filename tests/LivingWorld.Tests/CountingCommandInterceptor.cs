using Microsoft.EntityFrameworkCore.Diagnostics;

namespace LivingWorld.Tests;

/// <summary>Sensor do critério "zero round-trips de banco durante o tick" (task 11): conta todo
/// comando SQL executado. Registrado no <c>WorldDbContext</c> de teste — se algum sistema de
/// simulação tocar o banco durante o tick, o contador sobe fora da fronteira de snapshot.</summary>
public sealed class CountingCommandInterceptor : DbCommandInterceptor
{
    private int _count;
    public int Count => _count;

    public override InterceptionResult<int> NonQueryExecuting(
        System.Data.Common.DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
    {
        Interlocked.Increment(ref _count);
        return base.NonQueryExecuting(command, eventData, result);
    }

    public override InterceptionResult<System.Data.Common.DbDataReader> ReaderExecuting(
        System.Data.Common.DbCommand command, CommandEventData eventData, InterceptionResult<System.Data.Common.DbDataReader> result)
    {
        Interlocked.Increment(ref _count);
        return base.ReaderExecuting(command, eventData, result);
    }

    public override InterceptionResult<object> ScalarExecuting(
        System.Data.Common.DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
    {
        Interlocked.Increment(ref _count);
        return base.ScalarExecuting(command, eventData, result);
    }
}
