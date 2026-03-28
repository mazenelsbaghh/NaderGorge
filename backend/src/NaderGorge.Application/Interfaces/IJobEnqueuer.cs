using System.Text.Json;
using System.Threading.Tasks;

namespace NaderGorge.Application.Interfaces;

public interface IJobEnqueuer
{
    Task EnqueueJobAsync<T>(string queueName, string jobName, T data);
}
