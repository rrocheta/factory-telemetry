public interface IPublisher
{
    Task PublishJsonAsync(string topic, object payload, CancellationToken ct);
}