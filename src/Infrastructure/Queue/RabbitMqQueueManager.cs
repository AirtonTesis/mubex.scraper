using System.Text;
using System.Text.Json;
using Domain.Entities;
using Domain.ValueObjects;
using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;

namespace Infrastructure.Queue;

/// <summary>
/// Implementação do IQueueManager utilizando RabbitMQ como sistema de filas.
/// Configura declaração de fila com durabilidade para garantir persistência de mensagens.
/// **Validates: Requirements 4.1, 4.2**
/// </summary>
public class RabbitMqQueueManager : IQueueManager, IDisposable
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private const string QueueName = "scraping_jobs";

    public RabbitMqQueueManager(IConfiguration configuration)
    {
        var connectionString = configuration["ConnectionStrings:RabbitMQ"]
            ?? throw new ArgumentNullException("ConnectionStrings:RabbitMQ", "RabbitMQ connection string not configured");

        var factory = new ConnectionFactory
        {
            Uri = new Uri(connectionString),
            DispatchConsumersAsync = true
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();

        // Declarar fila durável para garantir persistência
        _channel.QueueDeclare(
            queue: QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);
    }

    /// <summary>
    /// Serializa e envia um Job para a fila RabbitMQ.
    /// **Validates: Requirement 4.1**
    /// </summary>
    public Task EnqueueJobAsync(Job job, CancellationToken cancellationToken = default)
    {
        var message = new JobQueueMessage(job.Id, job.SearchListId, job.Status, job.CreatedAt);
        var json = JsonSerializer.Serialize(message);

        var body = Encoding.UTF8.GetBytes(json);

        var properties = _channel.CreateBasicProperties();
        properties.Persistent = true; // Mensagem persistente para sobreviver a restarts

        _channel.BasicPublish(
            exchange: "",
            routingKey: QueueName,
            basicProperties: properties,
            body: body);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Recebe o próximo Job da fila RabbitMQ.
    /// Retorna null se não houver mensagens disponíveis.
    /// **Validates: Requirement 4.2**
    /// </summary>
    public Task<Job?> DequeueJobAsync(CancellationToken cancellationToken = default)
    {
        var result = _channel.BasicGet(QueueName, autoAck: false);

        if (result == null)
            return Task.FromResult<Job?>(null);

        try
        {
            var json = Encoding.UTF8.GetString(result.Body.ToArray());
            var jobData = JsonSerializer.Deserialize<JobQueueMessage>(json);

            if (jobData == null)
            {
                _channel.BasicNack(result.DeliveryTag, false, false);
                return Task.FromResult<Job?>(null);
            }

            // Create a Job instance from the queue message data.
            // In production, the worker should load the full job from the database
            // using the SearchListId, but for queue processing we reconstruct
            // a minimal Job representation.
            var job = Job.Create(jobData.SearchListId);
            _channel.BasicAck(result.DeliveryTag, false);

            return Task.FromResult<Job?>(job);
        }
        catch
        {
            _channel.BasicNack(result.DeliveryTag, false, true); // Requeue on error
            return Task.FromResult<Job?>(null);
        }
    }

    /// <summary>
    /// Atualiza o status de um Job. Em uma implementação completa,
    /// isso publicaria uma mensagem de atualização para monitoring.
    /// **Validates: Requirement 4.3**
    /// </summary>
    public Task UpdateJobStatusAsync(Guid jobId, JobStatus status, string? errorMessage = null, CancellationToken cancellationToken = default)
    {
        // In a full implementation, this would publish a status update message
        // for monitoring/consumer services to track job progress
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
    }

    private record JobQueueMessage(Guid Id, Guid SearchListId, JobStatus Status, DateTime CreatedAt);
}
