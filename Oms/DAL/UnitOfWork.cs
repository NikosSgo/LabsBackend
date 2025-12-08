using System.Data;
using Microsoft.Extensions.Options;
using Npgsql;
using WebApi.Config;
using WebApi.DAL.Models;

namespace WebApi.DAL;

public class UnitOfWork(IOptions<DbSettings> dbSettings): IDisposable
{
    private NpgsqlConnection _connection;
    private static NpgsqlDataSource _dataSource;
    
    public async Task<NpgsqlConnection> GetConnection(CancellationToken token)
    {
        if (_connection is not null)
        {
            return _connection;
        }
        
        _dataSource ??= CreateDataSource(dbSettings.Value);
       
        _connection = _dataSource.CreateConnection();
        _connection.StateChange += (sender, args) =>
        {
            if (args.CurrentState == ConnectionState.Closed)
                _connection = null;
        };
        
        await _connection.OpenAsync(token);

        return _connection;
    }

    public async ValueTask<NpgsqlTransaction> BeginTransactionAsync(CancellationToken token)
    {
        _connection ??= await GetConnection(token);
        return await _connection.BeginTransactionAsync(token);
    }

    public void Dispose()
    {
        DisposeConnection();
        GC.SuppressFinalize(this);
    }
    
    ~UnitOfWork()
    {
        DisposeConnection();
    }
    
    private void DisposeConnection()
    {
        _connection?.Dispose();
        _connection = null;
    }

    private static NpgsqlDataSource CreateDataSource(DbSettings settings)
    {
        var builder = new NpgsqlDataSourceBuilder(settings.ConnectionString);
        builder.MapComposite<V1OrderDal>("v1_order");
        builder.MapComposite<V1OrderItemDal>("v1_order_item");
        builder.MapComposite<V1AuditLogOrderDal>("v1_audit_log_order");
        return builder.Build();
    }
}
