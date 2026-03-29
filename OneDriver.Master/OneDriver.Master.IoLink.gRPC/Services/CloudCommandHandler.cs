namespace OneDriver.Master.IoLink.gRPC.Services
{
    public class CloudCommandHandler
    {
        private readonly ILogger<CloudCommandHandler> _logger;
        private readonly IoLinkMasterServiceImpl _masterService;
        private readonly AzureIoTHubService _iotHubService;
        private readonly IConfiguration _configuration;

        public CloudCommandHandler(
            ILogger<CloudCommandHandler> logger,
            IoLinkMasterServiceImpl masterService,
            AzureIoTHubService iotHubService,
            IConfiguration configuration)
        {
            _logger = logger;
            _masterService = masterService;
            _iotHubService = iotHubService;
            _configuration = configuration;

            _iotHubService.OnCommandReceived += HandleCommandAsync;
            _logger.LogInformation("CloudCommandHandler initialized and subscribed to command events");
        }

        private async Task HandleCommandAsync(CloudCommand command)
        {
            _logger.LogInformation("====> CloudCommandHandler: Processing command from cloud: Action={Action}, Parameter={ParameterName}", command.Action, command.ParameterName);

            try
            {
                var masterId = string.IsNullOrEmpty(command.MasterId) 
                    ? _configuration["IoLinkMaster:DefaultMasterId"] ?? "master-01" 
                    : command.MasterId;

                _logger.LogInformation("Using MasterId: {MasterId}", masterId);

                switch (command.Action.ToLowerInvariant())
                {
                    case "readparameter":
                    case "read":
                        _logger.LogInformation("Executing READ command for {ParameterName}", command.ParameterName);
                        await HandleReadParameterAsync(masterId, command.ParameterName, command.PortNumber);
                        break;

                    case "writeparameter":
                    case "write":
                        _logger.LogInformation("Executing WRITE command for {ParameterName}", command.ParameterName);
                        await HandleWriteParameterAsync(masterId, command.ParameterName, command.Value, command.PortNumber);
                        break;

                    case "writecommand":
                    case "command":
                        _logger.LogInformation("Executing COMMAND for {ParameterName}", command.ParameterName);
                        await HandleWriteCommandAsync(masterId, command.ParameterName, command.Value, command.PortNumber);
                        break;

                    default:
                        _logger.LogWarning("Unknown command action: {Action}", command.Action);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling cloud command");
            }
        }

        private async Task HandleReadParameterAsync(string masterId, string parameterName, int portNumber)
        {
            var request = new ReadParameterRequest
            {
                MasterId = masterId,
                ParameterName = parameterName,
                PortNumber = portNumber
            };

            var response = await _masterService.ReadParameter(request, null!);

            await _iotHubService.SendCommandResultAsync(
                masterId,
                "readParameter",
                parameterName,
                response.Variable?.Value,
                response.ErrorCode,
                response.ErrorMessage
            );

            _logger.LogInformation("Read parameter {ParameterName}: {Value} (ErrorCode: {ErrorCode})", 
                parameterName, response.Variable?.Value, response.ErrorCode);
        }

        private async Task HandleWriteParameterAsync(string masterId, string parameterName, string? value, int portNumber)
        {
            if (string.IsNullOrEmpty(value))
            {
                _logger.LogWarning("Write parameter command missing value");
                return;
            }

            var request = new WriteParameterRequest
            {
                MasterId = masterId,
                ParameterName = parameterName,
                Value = value,
                PortNumber = portNumber
            };

            var response = await _masterService.WriteParameter(request, null!);

            await _iotHubService.SendCommandResultAsync(
                masterId,
                "writeParameter",
                parameterName,
                value,
                response.ErrorCode,
                response.ErrorMessage
            );

            _logger.LogInformation("Write parameter {ParameterName} = {Value} (ErrorCode: {ErrorCode})", 
                parameterName, value, response.ErrorCode);
        }

        private async Task HandleWriteCommandAsync(string masterId, string commandName, string? value, int portNumber)
        {
            if (string.IsNullOrEmpty(value))
            {
                _logger.LogWarning("Write command missing value");
                return;
            }

            var request = new WriteCommandRequest
            {
                MasterId = masterId,
                CommandName = commandName,
                Value = value,
                PortNumber = portNumber
            };

            var response = await _masterService.WriteCommand(request, null!);

            await _iotHubService.SendCommandResultAsync(
                masterId,
                "writeCommand",
                commandName,
                value,
                response.ErrorCode,
                response.ErrorMessage
            );

            _logger.LogInformation("Write command {CommandName} = {Value} (ErrorCode: {ErrorCode})", 
                commandName, value, response.ErrorCode);
        }
    }
}
