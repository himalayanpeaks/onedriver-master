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
        private IoLinkMasterServiceImpl? _masterService;

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

        public void SetMasterService(IoLinkMasterServiceImpl masterService)
        {
            _masterService = masterService;
            _logger.LogInformation("Master service reference set for Direct Methods");
        }

        public async Task StartReceivingCommandsAsync()
        {
            if (_deviceClient == null || string.IsNullOrEmpty(_connectionString))
            {
                _logger.LogWarning("IoT Hub client not initialized - cannot receive commands");
                return;
            }

            const int maxRetries = 3;
            const int delayMilliseconds = 2000;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    _logger.LogInformation("Attempt {Attempt} of {MaxRetries} to start receiving commands", attempt, maxRetries);

                    // First, ensure the device is connected by opening the connection explicitly
                    await _deviceClient.OpenAsync();
                    _logger.LogInformation("Device client connection opened successfully");

                    // Add a small delay to ensure the MQTT connection is fully established
                    await Task.Delay(500);

                    // Set up C2D message handler
                    await _deviceClient.SetReceiveMessageHandlerAsync(ReceiveC2dMessageAsync, null);
                    _logger.LogInformation("Started listening for Cloud-to-Device messages from Azure");

                    // Set up Direct Method handlers
                    await _deviceClient.SetMethodHandlerAsync("ReadParameter", HandleReadParameterMethod, null);
                    await _deviceClient.SetMethodHandlerAsync("WriteParameter", HandleWriteParameterMethod, null);
                    await _deviceClient.SetMethodHandlerAsync("GetAllParameters", HandleGetAllParametersMethod, null);
                    await _deviceClient.SetMethodDefaultHandlerAsync(HandleDefaultMethod, null);

                    _logger.LogInformation("Started listening for Direct Methods from Azure");

                    // Success - exit retry loop
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to start receiving commands (attempt {Attempt}/{MaxRetries})", attempt, maxRetries);

                    if (attempt < maxRetries)
                    {
                        _logger.LogInformation("Retrying in {Delay}ms...", delayMilliseconds);
                        await Task.Delay(delayMilliseconds);

                        // Try to recreate the device client on network errors
                        if (ex is System.Net.Sockets.SocketException || 
                            ex.GetType().Name.Contains("IotHubCommunicationException"))
                        {
                            try
                            {
                                _deviceClient?.Dispose();
                                _deviceClient = DeviceClient.CreateFromConnectionString(_connectionString, TransportType.Mqtt);
                                _logger.LogInformation("Recreated device client after connection failure");
                            }
                            catch (Exception recreateEx)
                            {
                                _logger.LogError(recreateEx, "Failed to recreate device client");
                            }
                        }
                    }
                    else
                    {
                        _logger.LogError("All {MaxRetries} attempts to start receiving commands failed", maxRetries);
                    }
                }
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

        // Direct Method Handlers
        private async Task<MethodResponse> HandleReadParameterMethod(MethodRequest methodRequest, object userContext)
        {
            _logger.LogInformation("Direct Method 'ReadParameter' invoked");

            try
            {
                var payload = Encoding.UTF8.GetString(methodRequest.Data);
                var command = JsonSerializer.Deserialize<CloudCommand>(payload, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });

                if (command == null || string.IsNullOrEmpty(command.ParameterName))
                {
                    return new MethodResponse(Encoding.UTF8.GetBytes("{\"status\":\"error\",\"message\":\"Invalid request - ParameterName required\"}"), 400);
                }

                if (_masterService == null)
                {
                    return new MethodResponse(Encoding.UTF8.GetBytes("{\"status\":\"error\",\"message\":\"Master service not initialized\"}"), 500);
                }

                // Call the gRPC service method directly to get the value
                var request = new ReadParameterRequest
                {
                    MasterId = command.MasterId ?? "master-01",
                    ParameterName = command.ParameterName,
                    PortNumber = command.PortNumber
                };

                var response = await _masterService.ReadParameter(request, null!);

                if (response.ErrorCode == 0)
                {
                    // Also send to IoT Hub as telemetry
                    await SendCommandResultAsync(
                        request.MasterId,
                        "readParameter",
                        command.ParameterName,
                        response.Variable?.Value,
                        response.ErrorCode,
                        response.ErrorMessage
                    );

                    // Return the actual value in the Direct Method response
                    var result = new 
                    { 
                        status = "success", 
                        parameterName = command.ParameterName,
                        value = response.Variable?.Value ?? "",
                        dataType = response.Variable?.DataType ?? "",
                        errorCode = response.ErrorCode,
                        errorMessage = response.ErrorMessage
                    };
                    var resultJson = JsonSerializer.Serialize(result);
                    return new MethodResponse(Encoding.UTF8.GetBytes(resultJson), 200);
                }
                else
                {
                    var error = new 
                    { 
                        status = "error", 
                        parameterName = command.ParameterName,
                        errorCode = response.ErrorCode,
                        message = response.ErrorMessage 
                    };
                    return new MethodResponse(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(error)), 400);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling ReadParameter direct method");
                var error = new { status = "error", message = ex.Message };
                return new MethodResponse(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(error)), 500);
            }
        }

        private async Task<MethodResponse> HandleWriteParameterMethod(MethodRequest methodRequest, object userContext)
        {
            _logger.LogInformation("Direct Method 'WriteParameter' invoked");

            try
            {
                var payload = Encoding.UTF8.GetString(methodRequest.Data);
                var command = JsonSerializer.Deserialize<CloudCommand>(payload, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });

                if (command == null || string.IsNullOrEmpty(command.ParameterName) || string.IsNullOrEmpty(command.Value))
                {
                    return new MethodResponse(Encoding.UTF8.GetBytes("{\"status\":\"error\",\"message\":\"Invalid request - ParameterName and Value required\"}"), 400);
                }

                if (_masterService == null)
                {
                    return new MethodResponse(Encoding.UTF8.GetBytes("{\"status\":\"error\",\"message\":\"Master service not initialized\"}"), 500);
                }

                // Call the gRPC service method directly
                var request = new WriteParameterRequest
                {
                    MasterId = command.MasterId ?? "master-01",
                    ParameterName = command.ParameterName,
                    Value = command.Value,
                    PortNumber = command.PortNumber
                };

                var response = await _masterService.WriteParameter(request, null!);

                // Send to IoT Hub as telemetry
                await SendCommandResultAsync(
                    request.MasterId,
                    "writeParameter",
                    command.ParameterName,
                    command.Value,
                    response.ErrorCode,
                    response.ErrorMessage
                );

                if (response.ErrorCode == 0)
                {
                    var result = new 
                    { 
                        status = "success", 
                        parameterName = command.ParameterName,
                        value = command.Value,
                        message = "Parameter written successfully"
                    };
                    return new MethodResponse(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(result)), 200);
                }
                else
                {
                    var error = new 
                    { 
                        status = "error", 
                        parameterName = command.ParameterName,
                        errorCode = response.ErrorCode,
                        message = response.ErrorMessage 
                    };
                    return new MethodResponse(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(error)), 400);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling WriteParameter direct method");
                var error = new { status = "error", message = ex.Message };
                return new MethodResponse(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(error)), 500);
            }
        }

        private async Task<MethodResponse> HandleGetAllParametersMethod(MethodRequest methodRequest, object userContext)
        {
            _logger.LogInformation("Direct Method 'GetAllParameters' invoked");

            try
            {
                var payload = methodRequest.Data != null && methodRequest.Data.Length > 0
                    ? Encoding.UTF8.GetString(methodRequest.Data)
                    : "{}";

                var command = JsonSerializer.Deserialize<CloudCommand>(payload, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                }) ?? new CloudCommand();

                if (_masterService == null)
                {
                    return new MethodResponse(Encoding.UTF8.GetBytes("{\"status\":\"error\",\"message\":\"Master service not initialized\"}"), 500);
                }

                var masterId = command.MasterId ?? "master-01";

                // Get all parameter names
                var getAllRequest = new GetAllParametersRequest { MasterId = masterId };
                var getAllResponse = await _masterService.GetAllParameters(getAllRequest, null!);

                if (getAllResponse.ParameterNames.Count == 0)
                {
                    return new MethodResponse(Encoding.UTF8.GetBytes("{\"status\":\"error\",\"message\":\"No parameters found\"}"), 404);
                }

                // Read all parameter values
                var parameters = new List<object>();
                foreach (var paramName in getAllResponse.ParameterNames)
                {
                    try
                    {
                        var readRequest = new ReadParameterRequest
                        {
                            MasterId = masterId,
                            ParameterName = paramName,
                            PortNumber = 0
                        };

                        var readResponse = await _masterService.ReadParameter(readRequest, null!);

                        if (readResponse.ErrorCode == 0)
                        {
                            parameters.Add(new 
                            {
                                name = paramName,
                                value = readResponse.Variable?.Value ?? "",
                                dataType = readResponse.Variable?.DataType ?? ""
                            });

                            // Also send each to IoT Hub as telemetry
                            await SendTelemetryAsync(masterId, paramName, readResponse.Variable?.Value ?? "", 0);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to read parameter {ParameterName}", paramName);
                    }
                }

                // Send summary
                await SendCommandResultAsync(
                    masterId,
                    "getAllParameters",
                    "AllParameters",
                    $"{parameters.Count} parameters retrieved",
                    0,
                    $"Successfully retrieved {parameters.Count} parameters"
                );

                // Return all parameters in Direct Method response
                var result = new 
                { 
                    status = "success", 
                    count = parameters.Count,
                    parameters = parameters
                };

                return new MethodResponse(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(result)), 200);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling GetAllParameters direct method");
                var error = new { status = "error", message = ex.Message };
                return new MethodResponse(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(error)), 500);
            }
        }

        private Task<MethodResponse> HandleDefaultMethod(MethodRequest methodRequest, object userContext)
        {
            _logger.LogWarning("Unknown direct method called: {MethodName}", methodRequest.Name);

            var error = new { status = "error", message = $"Method '{methodRequest.Name}' not found" };
            var errorJson = JsonSerializer.Serialize(error);
            return Task.FromResult(new MethodResponse(Encoding.UTF8.GetBytes(errorJson), 404));
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
