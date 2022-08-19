using Confluent.Kafka;

namespace WorkerContagem.Extensions;

public static class KafkaExtensions
{
    public static IConsumer<Ignore, string> CreateConsumer(
        IConfiguration configuration)
    {
        var password = configuration["ApacheKafka:Password"];
        if (!String.IsNullOrWhiteSpace(password))
            return new ConsumerBuilder<Ignore, string>(
                new ConsumerConfig()
                {
                    BootstrapServers = configuration["ApacheKafka:Host"],
                    SecurityProtocol = SecurityProtocol.SaslSsl,
                    SaslMechanism = SaslMechanism.Plain,
                    SaslUsername = configuration["ApacheKafka:Username"],
                    SaslPassword = password,
                    GroupId = configuration["ApacheKafka:GroupId"],
                    AutoOffsetReset = AutoOffsetReset.Earliest
                }).Build();
        else
            return new ConsumerBuilder<Ignore, string>(
                new ConsumerConfig()
                {
                    BootstrapServers = configuration["ApacheKafka:Host"],
                    GroupId = configuration["ApacheKafka:GroupId"],
                    AutoOffsetReset = AutoOffsetReset.Earliest
                }).Build();
    }
}
