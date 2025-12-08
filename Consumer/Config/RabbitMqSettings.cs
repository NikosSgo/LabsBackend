namespace Consumer.Config;

public class RabbitMqSettings
{
    public string HostName { get; set; }
    public int Port { get; set; }
    public string UserName { get; set; }
    public string Password { get; set; }
    public string OrderCreatedQueue { get; set; }
    public int BatchSize  { get; set; }
    public int BatchTimeoutSeconds { get; set; }
}
