using System.Diagnostics;
using System.Text.Json;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Confluent.Kafka;
using WorkerContagem.Data;
using WorkerContagem.Extensions;
using WorkerContagem.Models;

namespace WorkerContagem;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IConfiguration _configuration;
    private readonly ContagemRepository _repository;
    private readonly string _topico;
    private readonly string _groupId;
    private readonly IConsumer<Ignore, string> _consumer;
    private readonly TelemetryConfiguration _telemetryConfig;
        
    public Worker(ILogger<Worker> logger,
        IConfiguration configuration,
        ContagemRepository repository,
        TelemetryConfiguration telemetryConfig)
    {
        _logger = logger;
        _configuration = configuration;
        _repository = repository;
        _telemetryConfig = telemetryConfig;
        _topico = _configuration["ApacheKafka:Topic"];
        _groupId = _configuration["ApacheKafka:GroupId"];
        _consumer = KafkaExtensions.CreateConsumer(_configuration);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation($"Topic = {_topico}");
        _logger.LogInformation($"Group Id = {_groupId}");
        _logger.LogInformation("Aguardando mensagens...");
        _consumer.Subscribe(_topico);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Run(() =>
            {
                var result = _consumer.Consume(stoppingToken);

                var inicio = DateTime.Now;
                var watch = new Stopwatch();
                watch.Start();

                var dadosContagem = result.Message.Value;
                
                watch.Stop();
                TelemetryClient client = new (_telemetryConfig);
                client.TrackDependency(
                    "Kafka", $"Consume {_topico}", dadosContagem, inicio, watch.Elapsed, true);

                _logger.LogInformation(
                    $"[{_topico} | {_groupId} | Nova mensagem] " +
                    dadosContagem);

                ProcessarResultado(dadosContagem, result.Partition.Value);
            });
        }
    }

    private void ProcessarResultado(string dados, int partition)
    {
        ResultadoContador? resultado;            
        try
        {
            resultado = JsonSerializer.Deserialize<ResultadoContador>(dados,
                new JsonSerializerOptions()
                {
                    PropertyNameCaseInsensitive = true
                });
        }
        catch
        {
            _logger.LogError("Dados inválidos para o Resultado");
            resultado = null;
        }

        if (resultado is not null)
        {
            try
            {
                _repository.Save(resultado, partition);
                _logger.LogInformation("Resultado registrado com sucesso!");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Erro durante a gravacao: {ex.Message}");
            }
        }
    }
}