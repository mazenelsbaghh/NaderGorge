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
        _connection = new Lazy<IConnectionMultiplexer>(() =>
            ConnectionMultiplexer.Connect(BuildConfiguration(config)));
    }

    public IConnectionMultiplexer GetConnection() => _connection.Value;
    public IDatabase GetDatabase() => _connection.Value.GetDatabase();

    public static ConfigurationOptions BuildConfiguration(IConfiguration config)
    {
        var sentinels = config["Redis:Sentinels"];
        var serviceName = config["Redis:SentinelServiceName"];
        if (!string.IsNullOrWhiteSpace(sentinels) && !string.IsNullOrWhiteSpace(serviceName))
        {
            var options = new ConfigurationOptions
            {
                ServiceName = serviceName,
                Password = config["Redis:Password"],
                AbortOnConnectFail = false,
                ConnectRetry = 5,
                ConnectTimeout = 10_000,
                SyncTimeout = 10_000,
            };
            foreach (var sentinel in sentinels.Split(
                         ',',
                         StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                options.EndPoints.Add(sentinel);
            }

            if (options.EndPoints.Count == 0)
            {
                throw new InvalidOperationException("Redis Sentinel endpoints are required.");
            }
            return options;
        }

        var connectionString = config["Redis:ConnectionString"]
            ?? config.GetConnectionString("Redis");
        if (string.IsNullOrWhiteSpace(connectionString)
            && string.Equals(
                config["ASPNETCORE_ENVIRONMENT"],
                "Production",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Redis Sentinel configuration is required in production.");
        }
        connectionString ??= "localhost:6379,abortConnect=false";
        return ConfigurationOptions.Parse(connectionString);
    }

    public void Dispose()
    {
        if (_connection.IsValueCreated)
            _connection.Value.Dispose();
    }
}
