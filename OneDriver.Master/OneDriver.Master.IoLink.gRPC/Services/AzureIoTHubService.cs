using Microsoft.Azure.Devices.Client;
using System.Text;
using System.Text.Json;

namespace OneDriver.Master.IoLink.gRPC.Services
{
    public class AzureIoTHubService : IDisposable
    {
        private readonly ILogger<AzureIoTHubService> _logger;
        private readonly string? _connectionString;
        private DeviceClient? _deviceClient;

        public event Func<CloudCommand, Task>? OnCommandReceived;

        public AzureIoTHubService(IConfiguration configuration, ILogger<AzureIoTHubService> logger)
        {
            _logger = logger;
            _connectionString = configuration["AzureIoTHub:ConnectionString"];

            if (!string.IsNullOrEmpty(_connectionString))
            {
                try
                {
                    _deviceClient = DeviceClient.CreateFromConnectionString(_connectionString, TransportType.Mqtt);
                    _logger.LogInformation("Azure IoT Hub client initialized successfully");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to initialize Azure IoT Hub client");
                }
            }
            else
            {
                _logger.LogWarning("Azure IoT Hub connection string not configured in appsettings.json");
            }
        }

        public async Task StartReceivingCommandsAsync()
        {
            if (_deviceClient == null || string.IsNullOrEmpty(_connectionString))
            {
                _logger.LogWarning("IoT Hub client not initialized - cannot receive commands");
                return;
            }

            try
            {
                await _deviceClient.SetReceiveMessageHandlerAsync(ReceiveC2dMessageAsync, null);
                _logger.LogInformation("Started listening for Cloud-to-Device commands from Azure");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start receiving commands");
            }
        }

        private async Task ReceiveC2dMessageAsync(Message receivedMessage, object? userContext)
        {
            try
            {
                var messageData = Encoding.UTF8.GetString(receivedMessage.GetBytes());
                _logger.LogInformation("Received C2D message from Azure: {Message}", messageData);

                var command = JsonSerializer.Deserialize<CloudCommand>(messageData, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });

                _logger.LogInformation("Deserialized command - Action: {Action}, ParameterName: {ParameterName}, Event handlers count: {Count}", 
                    command?.Action, command?.ParameterName, OnCommandReceived?.GetInvocationList().Length ?? 0);

                if (command != null && OnCommandReceived != null)
                {
                    _logger.LogInformation("Invoking command handler...");
                    await OnCommandReceived(command);
                    _logger.LogInformation("Command handler invoked successfully");
                }
                else
                {
                    if (command == null)
                        _logger.LogWarning("Command deserialization returned null");
                    if (OnCommandReceived == null)
                        _logger.LogWarning("No event handlers registered for OnCommandReceived");
                }

                await _deviceClient!.CompleteAsync(receivedMessage);
                _logger.LogInformation("C2D message completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing C2D message");
                try
                {
                    await _deviceClient!.RejectAsync(receivedMessage);
                }
                catch { }
            }
        }

        public async Task<bool> SendTelemetryAsync(string masterId, string parameterName, string value, int portNumber)
        {
            if (_deviceClient == null || string.IsNullOrEmpty(_connectionString))
            {
                _logger.LogDebug("IoT Hub client not initialized - telemetry not sent");
                return false;
            }

            try
            {
                var telemetryData = new
                {
                    masterId,
                    portNumber,
                    parameterName,
                    value,
                    timestamp = DateTimeOffset.UtcNow
                };

                var messageString = JsonSerializer.Serialize(telemetryData);

                var message = new Message(Encoding.UTF8.GetBytes(messageString))
                {
                    ContentType = "application/json",
                    ContentEncoding = "utf-8"
                };
                await _deviceClient.SendEventAsync(message);

                _logger.LogDebug("Telemetry sent to IoT Hub: {MasterId}/{ParameterName}", masterId, parameterName);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send telemetry to IoT Hub");
                return false;
            }
        }

        public async Task<bool> SendCommandResultAsync(string masterId, string action, string parameterName, string? value, int errorCode, string errorMessage)
        {
            if (_deviceClient == null || string.IsNullOrEmpty(_connectionString))
            {
                _logger.LogDebug("IoT Hub client not initialized - command result not sent");
                return false;
            }

            try
            {
                var resultData = new
                {
                    masterId,
                    action,
                    parameterName,
                    value,
                    errorCode,
                    errorMessage,
                    timestamp = DateTimeOffset.UtcNow
                };

                var messageString = JsonSerializer.Serialize(resultData);

                var message = new Message(Encoding.UTF8.GetBytes(messageString))
                {
                    ContentType = "application/json",
                    ContentEncoding = "utf-8"
                };
                await _deviceClient.SendEventAsync(message);

                _logger.LogInformation("Command result sent to IoT Hub: {Action} {ParameterName} = {Value}", action, parameterName, value);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send command result to IoT Hub");
                return false;
            }
        }

        public async Task<bool> SendProcessDataAsync(string masterId, int channelNumber, int index, int subindex, byte[] data)
        {
            if (_deviceClient == null || string.IsNullOrEmpty(_connectionString))
            {
                _logger.LogDebug("IoT Hub client not initialized - process data not sent");
                return false;
            }

            try
            {
                var telemetryData = new
                {
                    masterId,
                    channelNumber,
                    index,
                    subindex,
                    data = Convert.ToBase64String(data),
                    timestamp = DateTimeOffset.UtcNow
                };

                var messageString = JsonSerializer.Serialize(telemetryData);

                var message = new Message(Encoding.UTF8.GetBytes(messageString))
                {
                    ContentType = "application/json",
                    ContentEncoding = "utf-8"
                };
                await _deviceClient.SendEventAsync(message);

                _logger.LogDebug("Process data sent to IoT Hub: {MasterId}/Ch{ChannelNumber}", masterId, channelNumber);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send process data to IoT Hub");
                return false;
            }
        }

        public void Dispose()
        {
            if (_deviceClient is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }

    public class CloudCommand
    {
        public string Action { get; set; } = string.Empty;
        public string MasterId { get; set; } = string.Empty;
        public string ParameterName { get; set; } = string.Empty;
        public string? Value { get; set; }
        public int PortNumber { get; set; }
    }
}
