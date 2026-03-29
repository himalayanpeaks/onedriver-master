using DeviceDescriptor.Factory;
using DeviceDescriptor.IoLink.Source;
using OneDriver.Master.Factory;
using OneDriver.Master.IoLink;
using OneDriver.Master.IoLink.Products;

namespace OneDriver.Master.IoLink.gRPC.Services
{
    public class IoLinkMasterHostedService : IHostedService
    {
        private readonly ILogger<IoLinkMasterHostedService> _logger;
        private readonly IConfiguration _configuration;
        private readonly IoLinkMasterServiceImpl _masterService;
        private readonly AzureIoTHubService _iotHubService;
        private readonly CloudCommandHandler _commandHandler;

        public IoLinkMasterHostedService(
            ILogger<IoLinkMasterHostedService> logger,
            IConfiguration configuration,
            IoLinkMasterServiceImpl masterService,
            AzureIoTHubService iotHubService,
            CloudCommandHandler commandHandler)
        {
            _logger = logger;
            _configuration = configuration;
            _masterService = masterService;
            _iotHubService = iotHubService;
            _commandHandler = commandHandler;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                var autoConnect = _configuration.GetValue<bool>("IoLinkMaster:AutoConnectOnStartup");
                if (!autoConnect)
                {
                    _logger.LogInformation("Auto-connect is disabled");
                    return;
                }

                _logger.LogInformation("Configuring IODD Finder...");
                var baseUrl = _configuration["IODDFinder:BaseUrl"];
                var apiKey = _configuration["IODDFinder:ApiKey"];
                
                if (string.IsNullOrEmpty(baseUrl) || string.IsNullOrEmpty(apiKey))
                {
                    _logger.LogWarning("IODD Finder configuration missing");
                    return;
                }

                DeviceDescriptorFactory.ConfigureIoddFinder(baseUrl, apiKey);

                _logger.LogInformation("Creating device descriptor...");
                var descriptorConfig = _configuration.GetSection("IoLinkMaster:Descriptor");
                var descriptorRequest = new DescriptorRequest
                {
                    Address = descriptorConfig["Address"] ?? "",
                    DeviceId = descriptorConfig["DeviceId"] ?? "",
                    ProductName = descriptorConfig["ProductName"] ?? "",
                    IoLinkRevision = descriptorConfig["IoLinkRevision"] ?? "",
                    ArticleNumber = descriptorConfig["ArticleNumber"] ?? ""
                };

                var descriptor = DeviceDescriptorFactory.CreateIoLinkDescriptor(DescriptorType.IoddFinder, descriptorRequest);
                _logger.LogInformation("Descriptor created with {Count} parameters", descriptor.Variables.ParamsCollection.Count);

                _logger.LogInformation("Creating IO-Link Master...");
                var deviceHAL = new TmgMaster2();
                var device = MasterFactory.CreateIoLinkMaster(MasterType.TmgMaster2, deviceHAL, descriptor);
                
                if (device == null)
                {
                    _logger.LogError("Failed to create IO-Link master");
                    return;
                }

                var masterId = _configuration["IoLinkMaster:DefaultMasterId"] ?? "master-01";
                _masterService.RegisterDevice(masterId, device, descriptor);

                _logger.LogInformation("Connecting to master device...");
                var comPort = _configuration["IoLinkMaster:ComPort"] ?? "COM3";
                var errorCode = device.Connect(comPort);

                if (errorCode != 0)
                {
                    _logger.LogError("Failed to connect to master: {Error}", device.GetErrorMessage(errorCode));
                    return;
                }

                _logger.LogInformation("Successfully connected to master at {ComPort}", comPort);

                _logger.LogInformation("Selecting sensor at port 0...");
                var selectError = device.SelectSensorAtPort(0);
                if (selectError != OneDriver.Master.Abstract.Contracts.Definition.Error.NoError)
                {
                    _logger.LogError("Failed to select sensor at port 0: {Error}", selectError);
                    device.Disconnect();
                    return;
                }

                _logger.LogInformation("Connecting to sensor...");
                var sensorErrorCode = device.ConnectSensor();
                if (sensorErrorCode != 0)
                {
                    _logger.LogError("Failed to connect to sensor: {Error}", device.GetErrorMessage(sensorErrorCode));
                    device.Disconnect();
                    return;
                }

                _logger.LogInformation("Successfully connected to sensor at port 0");
                _logger.LogInformation("Master {MasterId} is ready. Data will be sent to Azure IoT Hub on parameter reads.", masterId);

                _logger.LogInformation("Starting Cloud-to-Device command listener...");
                await _iotHubService.StartReceivingCommandsAsync();
                _logger.LogInformation("Cloud command handler is ready (injected: {HandlerReady})", _commandHandler != null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during master initialization");
            }

            await Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping IO-Link Master service");
            return Task.CompletedTask;
        }
    }
}
