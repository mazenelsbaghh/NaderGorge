using StackExchange.Redis;
using Microsoft.Extensions.Configuration;

namespace NaderGorge.Infrastructure.Cache;

public interface IRedisConnectionFactory
{
    IConnectionMultiplexer GetConnection();
    IDatabase GetDatabase();
}

public class RedisConnectionFactory : IRedisConnectionFactory, IDisposable
{
    private readonly Lazy<IConnectionMultiplexer> _connection;

    public RedisConnectionFactory(IConfiguration config)
    {
        var connectionString = config["Redis:ConnectionString"]
            ?? config.GetConnectionString("Redis")
            ?? "localhost:6379,abortConnect=false";
        _connection = new Lazy<IConnectionMultiplexer>(() =>
            ConnectionMultiplexer.Connect(connectionString));
    }

    public IConnectionMultiplexer GetConnection() => _connection.Value;
    public IDatabase GetDatabase() => _connection.Value.GetDatabase();

    public void Dispose()
    {
        if (_connection.IsValueCreated)
            _connection.Value.Dispose();
    }
}
