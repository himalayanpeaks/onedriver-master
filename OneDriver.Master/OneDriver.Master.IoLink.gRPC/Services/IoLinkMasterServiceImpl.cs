using DeviceDescriptor.Abstract;
using DeviceDescriptor.Factory;
using DeviceDescriptor.IoLink;
using DeviceDescriptor.IoLink.Source;
using DeviceDescriptor.IoLink.Variables;
using Grpc.Core;
using OneDriver.Master.Abstract;
using OneDriver.Master.Factory;
using OneDriver.Master.IoLink;
using OneDriver.Master.IoLink.Channels;
using OneDriver.Master.IoLink.Products;
using System.Collections.Concurrent;
using System.Text;

namespace OneDriver.Master.IoLink.gRPC.Services
{
    public class IoLinkMasterServiceImpl : IoLinkMasterService.IoLinkMasterServiceBase
    {
        private readonly ILogger<IoLinkMasterServiceImpl> _logger;
        private readonly AzureIoTHubService _iotHubService;
        private readonly ConcurrentDictionary<string, Device> _devices = new();
        private readonly ConcurrentDictionary<string, BasicDescriptor<Variable>> _descriptors = new();

        public IoLinkMasterServiceImpl(ILogger<IoLinkMasterServiceImpl> logger, AzureIoTHubService iotHubService)
        {
            _logger = logger;
            _iotHubService = iotHubService;
        }

        public void RegisterDevice(string masterId, Device device, BasicDescriptor<Variable> descriptor)
        {
            _devices[masterId] = device;
            _descriptors[masterId] = descriptor;
            _logger.LogInformation("Device {MasterId} registered with descriptor", masterId);
        }

        public override Task<ConnectResponse> Connect(ConnectRequest request, ServerCallContext context)
        {
            try
            {
                if (!_devices.TryGetValue(request.MasterId, out var device))
                {
                    var descriptor = DeviceDescriptorFactory.CreateIoLinkDescriptor(DescriptorType.IoddFinder, new DescriptorRequest());
                    if (descriptor == null)
                    {
                        return Task.FromResult(new ConnectResponse
                        {
                            ErrorCode = -1,
                            ErrorMessage = "Failed to create descriptor",
                            IsConnected = false
                        });
                    }

                    var deviceHAL = new TmgMaster2();
                    device = MasterFactory.CreateIoLinkMaster(MasterType.TmgMaster2, deviceHAL, descriptor);

                    if (device == null)
                    {
                        return Task.FromResult(new ConnectResponse
                        {
                            ErrorCode = -1,
                            ErrorMessage = "Failed to create device",
                            IsConnected = false
                        });
                    }

                    _devices[request.MasterId] = device;
                    _descriptors[request.MasterId] = descriptor;
                }

                var errorCode = device.Connect(request.InitString);
                return Task.FromResult(new ConnectResponse
                {
                    ErrorCode = errorCode,
                    ErrorMessage = errorCode != 0 ? device.GetErrorMessage(errorCode) : "Connected",
                    IsConnected = errorCode == 0
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error connecting to master {MasterId}", request.MasterId);
                return Task.FromResult(new ConnectResponse
                {
                    ErrorCode = -1,
                    ErrorMessage = ex.Message,
                    IsConnected = false
                });
            }
        }

        public override Task<DisconnectResponse> Disconnect(DisconnectRequest request, ServerCallContext context)
        {
            try
            {
                if (!_devices.TryGetValue(request.MasterId, out var device))
                {
                    return Task.FromResult(new DisconnectResponse
                    {
                        ErrorCode = -1,
                        ErrorMessage = "Device not found"
                    });
                }

                var errorCode = device.Disconnect();
                return Task.FromResult(new DisconnectResponse
                {
                    ErrorCode = errorCode,
                    ErrorMessage = errorCode != 0 ? device.GetErrorMessage(errorCode) : "Disconnected"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disconnecting master {MasterId}", request.MasterId);
                return Task.FromResult(new DisconnectResponse
                {
                    ErrorCode = -1,
                    ErrorMessage = ex.Message
                });
            }
        }

        public override Task<SelectSensorResponse> SelectSensorAtPort(SelectSensorRequest request, ServerCallContext context)
        {
            try
            {
                if (!_devices.TryGetValue(request.MasterId, out var device))
                {
                    return Task.FromResult(new SelectSensorResponse
                    {
                        ErrorCode = -1,
                        ErrorMessage = "Device not found"
                    });
                }

                var error = device.SelectSensorAtPort(request.PortNumber);
                return Task.FromResult(new SelectSensorResponse
                {
                    ErrorCode = (int)error,
                    ErrorMessage = error.ToString()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error selecting sensor at port {Port}", request.PortNumber);
                return Task.FromResult(new SelectSensorResponse
                {
                    ErrorCode = -1,
                    ErrorMessage = ex.Message
                });
            }
        }

        public override Task<ConnectSensorResponse> ConnectSensor(ConnectSensorRequest request, ServerCallContext context)
        {
            try
            {
                if (!_devices.TryGetValue(request.MasterId, out var device))
                {
                    return Task.FromResult(new ConnectSensorResponse
                    {
                        ErrorCode = -1,
                        ErrorMessage = "Device not found",
                        IsSensorConnected = false
                    });
                }

                if (request.PortNumber > 0)
                {
                    device.SelectSensorAtPort(request.PortNumber);
                }

                var errorCode = device.ConnectSensor();
                return Task.FromResult(new ConnectSensorResponse
                {
                    ErrorCode = errorCode,
                    ErrorMessage = errorCode != 0 ? device.GetErrorMessage(errorCode) : "Sensor connected",
                    IsSensorConnected = errorCode == 0
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error connecting sensor");
                return Task.FromResult(new ConnectSensorResponse
                {
                    ErrorCode = -1,
                    ErrorMessage = ex.Message,
                    IsSensorConnected = false
                });
            }
        }

        public override Task<DisconnectSensorResponse> DisconnectSensor(DisconnectSensorRequest request, ServerCallContext context)
        {
            try
            {
                if (!_devices.TryGetValue(request.MasterId, out var device))
                {
                    return Task.FromResult(new DisconnectSensorResponse
                    {
                        ErrorCode = -1,
                        ErrorMessage = "Device not found"
                    });
                }

                var errorCode = device.DisconnectSensor();
                return Task.FromResult(new DisconnectSensorResponse
                {
                    ErrorCode = errorCode,
                    ErrorMessage = errorCode != 0 ? device.GetErrorMessage(errorCode) : "Sensor disconnected"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disconnecting sensor");
                return Task.FromResult(new DisconnectSensorResponse
                {
                    ErrorCode = -1,
                    ErrorMessage = ex.Message
                });
            }
        }

        public override async Task<ReadParameterResponse> ReadParameter(ReadParameterRequest request, ServerCallContext context)
        {
            try
            {
                if (!_devices.TryGetValue(request.MasterId, out var device))
                {
                    return new ReadParameterResponse
                    {
                        ErrorCode = -1,
                        ErrorMessage = "Device not found"
                    };
                }

                if (!_descriptors.TryGetValue(request.MasterId, out var descriptor))
                {
                    return new ReadParameterResponse
                    {
                        ErrorCode = -1,
                        ErrorMessage = "Descriptor not found"
                    };
                }

                if (request.PortNumber > 0)
                {
                    device.SelectSensorAtPort(request.PortNumber);
                }

                var errorCode = device.ReadParameterFromSensor(request.ParameterName, out string? value);

                var variable = descriptor.Variables.ParamsCollection
                    .FirstOrDefault(v => v.Name == request.ParameterName);

                var response = new ReadParameterResponse
                {
                    ErrorCode = errorCode,
                    ErrorMessage = errorCode != 0 ? device.GetErrorMessage(errorCode) : "Success"
                };

                if (errorCode == 0)
                {
                    if (variable != null)
                    {
                        // Some CHAR values are only exposed via raw index/subindex reads.
                        // Fall back to ReadParam when the named read returns an empty string.
                        if (string.IsNullOrEmpty(value)
                            && string.Equals(variable.DataType.ToString(), "CHAR", StringComparison.OrdinalIgnoreCase)
                            && variable.Index > 0)
                        {
                            var rawError = device.ReadParam(variable.Index, variable.Subindex, out byte[]? rawData);
                            if (rawError == 0 && rawData != null && rawData.Length > 0)
                            {
                                value = Encoding.ASCII.GetString(rawData).TrimEnd('\0', ' ');
                            }
                        }

                        response.Variable = MapVariableToProto(variable);
                        response.Variable.Value = value ?? string.Empty;
                    }

                    // Send telemetry to Azure IoT Hub
                    if (!string.IsNullOrEmpty(value))
                    {
                        await _iotHubService.SendTelemetryAsync(request.MasterId, request.ParameterName, value, request.PortNumber);
                    }
                }

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading parameter {ParameterName}", request.ParameterName);
                return new ReadParameterResponse
                {
                    ErrorCode = -1,
                    ErrorMessage = ex.Message
                };
            }
        }

        public override Task<WriteParameterResponse> WriteParameter(WriteParameterRequest request, ServerCallContext context)
        {
            try
            {
                if (!_devices.TryGetValue(request.MasterId, out var device))
                {
                    return Task.FromResult(new WriteParameterResponse
                    {
                        ErrorCode = -1,
                        ErrorMessage = "Device not found"
                    });
                }

                if (request.PortNumber > 0)
                {
                    device.SelectSensorAtPort(request.PortNumber);
                }

                var errorCode = device.WriteParameterToSensor(request.ParameterName, request.Value);
                return Task.FromResult(new WriteParameterResponse
                {
                    ErrorCode = errorCode,
                    ErrorMessage = errorCode != 0 ? device.GetErrorMessage(errorCode) : "Success"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error writing parameter {ParameterName}", request.ParameterName);
                return Task.FromResult(new WriteParameterResponse
                {
                    ErrorCode = -1,
                    ErrorMessage = ex.Message
                });
            }
        }

        public override Task<WriteCommandResponse> WriteCommand(WriteCommandRequest request, ServerCallContext context)
        {
            try
            {
                if (!_devices.TryGetValue(request.MasterId, out var device))
                {
                    return Task.FromResult(new WriteCommandResponse
                    {
                        ErrorCode = -1,
                        ErrorMessage = "Device not found"
                    });
                }

                if (request.PortNumber > 0)
                {
                    device.SelectSensorAtPort(request.PortNumber);
                }

                var errorCode = device.WriteCommandToSensor(request.CommandName, request.Value);
                return Task.FromResult(new WriteCommandResponse
                {
                    ErrorCode = errorCode,
                    ErrorMessage = errorCode != 0 ? device.GetErrorMessage(errorCode) : "Success"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error writing command {CommandName}", request.CommandName);
                return Task.FromResult(new WriteCommandResponse
                {
                    ErrorCode = -1,
                    ErrorMessage = ex.Message
                });
            }
        }

        public override Task<GetAllParametersResponse> GetAllParameters(GetAllParametersRequest request, ServerCallContext context)
        {
            try
            {
                if (!_devices.TryGetValue(request.MasterId, out var device))
                {
                    return Task.FromResult(new GetAllParametersResponse());
                }

                if (request.PortNumber > 0)
                {
                    device.SelectSensorAtPort(request.PortNumber);
                }

                var paramNames = device.GetAllParamsFromSensor();
                var response = new GetAllParametersResponse();
                response.ParameterNames.AddRange(paramNames);
                
                return Task.FromResult(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all parameters");
                return Task.FromResult(new GetAllParametersResponse());
            }
        }

        public override Task<ReadParamByIndexResponse> ReadParamByIndex(ReadParamByIndexRequest request, ServerCallContext context)
        {
            try
            {
                if (!_devices.TryGetValue(request.MasterId, out var device))
                {
                    return Task.FromResult(new ReadParamByIndexResponse
                    {
                        ErrorCode = -1,
                        ErrorMessage = "Device not found"
                    });
                }

                var errorCode = device.ReadParam(request.Index, request.Subindex, out byte[]? data);
                return Task.FromResult(new ReadParamByIndexResponse
                {
                    ErrorCode = errorCode,
                    ErrorMessage = errorCode != 0 ? device.GetErrorMessage(errorCode) : "Success",
                    Data = data != null ? Google.Protobuf.ByteString.CopyFrom(data) : Google.Protobuf.ByteString.Empty
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading param at index {Index}/{Subindex}", request.Index, request.Subindex);
                return Task.FromResult(new ReadParamByIndexResponse
                {
                    ErrorCode = -1,
                    ErrorMessage = ex.Message
                });
            }
        }

        public override Task<WriteParamByIndexResponse> WriteParamByIndex(WriteParamByIndexRequest request, ServerCallContext context)
        {
            try
            {
                if (!_devices.TryGetValue(request.MasterId, out var device))
                {
                    return Task.FromResult(new WriteParamByIndexResponse
                    {
                        ErrorCode = -1,
                        ErrorMessage = "Device not found"
                    });
                }

                var errorCode = device.WriteParam(request.Index, request.Subindex, request.Data.ToByteArray());
                return Task.FromResult(new WriteParamByIndexResponse
                {
                    ErrorCode = errorCode,
                    ErrorMessage = errorCode != 0 ? device.GetErrorMessage(errorCode) : "Success"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error writing param at index {Index}/{Subindex}", request.Index, request.Subindex);
                return Task.FromResult(new WriteParamByIndexResponse
                {
                    ErrorCode = -1,
                    ErrorMessage = ex.Message
                });
            }
        }

        public override Task<GetDescriptorResponse> GetDescriptor(GetDescriptorRequest request, ServerCallContext context)
        {
            try
            {
                if (!_devices.TryGetValue(request.MasterId, out var device))
                {
                    return Task.FromResult(new GetDescriptorResponse());
                }

                if (!_descriptors.TryGetValue(request.MasterId, out var descriptor))
                {
                    return Task.FromResult(new GetDescriptorResponse());
                }

                var response = new GetDescriptorResponse
                {
                    ParameterCount = descriptor.Variables.ParamsCollection.Count,
                    CommandCount = descriptor.Variables.CommandsCollection.Count,
                    ProcessDataCount = descriptor.Variables.PdInCollection.Count + descriptor.Variables.PdOutCollection.Count
                };

                var seenEntries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                void AddToParameters(IEnumerable<Variable>? source, string tabKind)
                {
                    if (source == null)
                    {
                        return;
                    }

                    foreach (var variable in source)
                    {
                        var key = $"{tabKind}:{variable.Name}";
                        if (!seenEntries.Add(key))
                        {
                            continue;
                        }

                        response.Parameters.Add(MapVariableToProto(variable, tabKind));
                    }
                }

                // Params are split by their standard/specific/system collections.
                AddToParameters(descriptor.Variables.StandardVariables, "Standard Params");
                AddToParameters(descriptor.Variables.SpecificVariables, "Specific");
                AddToParameters(descriptor.Variables.SystemVariables, "System");

                // Expose non-parameter collections as dedicated tab kinds.
                AddToParameters(descriptor.Variables.CommandsCollection, "Commands");
                AddToParameters(descriptor.Variables.EventsCollection, "Events");
                AddToParameters(descriptor.Variables.PdInCollection, "PdIn");
                AddToParameters(descriptor.Variables.PdOutCollection, "PdOut");

                foreach (var param in descriptor.Variables.ParamsCollection)
                {
                    var key = $"params:{param.Name}";
                    if (!seenEntries.Add(key))
                    {
                        continue;
                    }

                    response.Parameters.Add(MapVariableToProto(param));
                }

                foreach (var cmd in descriptor.Variables.CommandsCollection)
                {
                    response.Commands.Add(MapVariableToProto(cmd, "Commands"));
                }

                return Task.FromResult(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting descriptor for {MasterId}", request.MasterId);
                return Task.FromResult(new GetDescriptorResponse());
            }
        }

        public override Task<GetChannelInfoResponse> GetChannelInfo(GetChannelInfoRequest request, ServerCallContext context)
        {
            try
            {
                if (!_devices.TryGetValue(request.MasterId, out var device))
                {
                    return Task.FromResult(new GetChannelInfoResponse());
                }

                if (request.ChannelNumber >= device.Elements.Count)
                {
                    return Task.FromResult(new GetChannelInfoResponse());
                }

                var channel = device.Elements[request.ChannelNumber];
                var channelParams = (ChannelParams)channel.Parameters;

                var response = new GetChannelInfoResponse
                {
                    ChannelName = channelParams.Name,
                    IsActive = channelParams.IsActive
                };

                return Task.FromResult(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting channel info for channel {ChannelNumber}", request.ChannelNumber);
                return Task.FromResult(new GetChannelInfoResponse());
            }
        }
        public override async Task StreamProcessData(StreamProcessDataRequest request, IServerStreamWriter<ProcessDataUpdate> responseStream, ServerCallContext context)
        {
            try
            {
                if (!_devices.TryGetValue(request.MasterId, out var device))
                {
                    _logger.LogError("Device {MasterId} not found for process data streaming", request.MasterId);
                    return;
                }

                if (request.PortNumber > 0)
                {
                    device.SelectSensorAtPort(request.PortNumber);
                }

                //_logger.LogInformation("Starting process data streaming for {MasterId}, port {PortNumber}", request.MasterId, request.PortNumber);

                var taskCompletionSource = new TaskCompletionSource<bool>();

                void ProcessDataHandler(object? sender, IoLink.Events.ProcessDataEventArgs e)
                {
                    var update = new ProcessDataUpdate
                    {
                        ChannelNumber = e.ChannelNumber,
                        Index = e.Parameter.Index,
                        Subindex = e.Parameter.Subindex,
                        Data = Google.Protobuf.ByteString.CopyFrom(System.Text.Encoding.UTF8.GetBytes(e.Parameter.Value ?? string.Empty)),
                        Timestamp = new DateTimeOffset(e.TimeStamp).ToUnixTimeMilliseconds(),
                        ParameterName = e.Parameter.Name ?? string.Empty,
                        DisplayName = e.Parameter.DisplayName ?? string.Empty,
                        Value = e.Parameter.Value ?? string.Empty,
                        DataType = e.Parameter.DataType.ToString(),
                        Minimum = e.Parameter.Minimum ?? string.Empty,
                        Maximum = e.Parameter.Maximum ?? string.Empty,
                        LengthInBits = e.Parameter.LengthInBits,
                        IsParameter = true,
                        IsProcessData = true
                    };

                    try
                    {
                        responseStream.WriteAsync(update).Wait();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error writing process data to stream");
                        taskCompletionSource.TrySetResult(false);
                    }
                }

                void EventHandler(object? sender, IoLink.Events.IoLinkEventArgs e)
                {
                    // Send events to stream independently from process data
                    var eventUpdate = new ProcessDataUpdate
                    {
                        ChannelNumber = e.ChannelNumber,
                        Timestamp = new DateTimeOffset(e.TimeStamp).ToUnixTimeMilliseconds(),

                        // Event-specific fields
                        EventNumber = e.EventNumber,
                        EventCode = e.EventCode,
                        EventInstance = (uint)e.Instance,
                        EventMode = (uint)e.Mode,
                        EventType = (uint)e.Type,
                        EventPdValid = e.PdValid != 0,
                        EventLocalGenerated = e.LocalGenerated != 0,
                        EventSensorStatus = e.SensorStatus,
                        EventName = GetEventName(e.EventCode),
                        EventDescription = GetEventDescription(e.EventCode, e.Type),
                        EventSeverity = GetEventSeverity(e.Type),

                        // Mark as event-only (no process data)
                        IsParameter = false,
                        IsProcessData = false
                    };

                    try
                    {
                        responseStream.WriteAsync(eventUpdate).Wait();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error writing event to stream");
                        taskCompletionSource.TrySetResult(false);
                    }
                }

                device.ProcessDataReceived += ProcessDataHandler;
                device.IoLinkEventReceived += EventHandler;

                try
                {
                    context.CancellationToken.Register(() => taskCompletionSource.TrySetResult(true));
                    await taskCompletionSource.Task;
                }
                finally
                {
                    device.ProcessDataReceived -= ProcessDataHandler;
                    device.IoLinkEventReceived -= EventHandler;
                    //_logger.LogInformation("Stopped process data streaming for {MasterId}", request.MasterId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in StreamProcessData for {MasterId}", request.MasterId);
            }
        }
        
        private VariableData MapVariableToProto(DeviceDescriptor.IoLink.Variables.Variable variable, string? variableKindOverride = null)
        {
            var result = new VariableData
            {
                // Core identification
                Name = variable.Name ?? string.Empty,
                Index = variable.Index,
                Subindex = variable.Subindex,

                // Data value and type information
                Value = variable.Value ?? string.Empty,
                DataType = variable.DataType.ToString(),
                LengthInBits = variable.LengthInBits,
                Offset = variable.Offset,
                ArrayCount = variable.ArrayCount,

                // Variable classification
                VariableKind = string.IsNullOrWhiteSpace(variableKindOverride) ? variable.Kind.ToString() : variableKindOverride,
                IsDynamic = variable.IsDynamic,

                // Display and identification
                DisplayName = variable.DisplayName ?? string.Empty,
                TextId = variable.TextId ?? string.Empty,
                OwnerId = variable.OwnerId ?? string.Empty,

                // Value constraints and validation
                DefaultValue = variable.Default ?? string.Empty,
                Minimum = variable.Minimum ?? string.Empty,
                Maximum = variable.Maximum ?? string.Empty,
                Valid = variable.Valid ?? string.Empty,

                // Access control
                Access = variable.Access.ToString()
            };

            // Map ValidDisplayTexts dictionary
            if (variable.ValidDisplayTexts != null)
            {
                foreach (var kvp in variable.ValidDisplayTexts)
                {
                    result.ValidDisplayTexts.Add(kvp.Key, kvp.Value);
                }
            }

            // Check if this is actually a MenuVariable and map additional properties
            var menuVariableType = variable.GetType();
            if (menuVariableType.Name == "MenuVariable")
            {
                try
                {
                    // Use reflection to get MenuVariable-specific properties
                    var accessRightRestriction = menuVariableType.GetProperty("AccessRightRestriction")?.GetValue(variable);
                    result.AccessRightRestriction = accessRightRestriction?.ToString() ?? string.Empty;

                    var menuId = menuVariableType.GetProperty("MenuId")?.GetValue(variable) as string;
                    result.MenuId = menuId ?? string.Empty;

                    var menuPath = menuVariableType.GetProperty("MenuPath")?.GetValue(variable) as string;
                    result.MenuPath = menuPath ?? string.Empty;

                    var role = menuVariableType.GetProperty("Role")?.GetValue(variable) as string;
                    result.Role = role ?? string.Empty;

                    var displayFormat = menuVariableType.GetProperty("DisplayFormat")?.GetValue(variable) as string;
                    result.DisplayFormat = displayFormat ?? string.Empty;

                    var unitCode = menuVariableType.GetProperty("UnitCode")?.GetValue(variable) as string;
                    result.UnitCode = unitCode ?? string.Empty;

                    var gradient = menuVariableType.GetProperty("Gradient")?.GetValue(variable) as string;
                    result.Gradient = gradient ?? string.Empty;

                    var scalingOffset = menuVariableType.GetProperty("ScalingOffset")?.GetValue(variable) as string;
                    result.ScalingOffset = scalingOffset ?? string.Empty;

                    var buttonValue = menuVariableType.GetProperty("ButtonValue")?.GetValue(variable) as string;
                    result.ButtonValue = buttonValue ?? string.Empty;

                    var isSubindexSupported = menuVariableType.GetProperty("IsSubindexSupported")?.GetValue(variable);
                    result.IsSubindexSupported = isSubindexSupported is bool b && b;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to map MenuVariable properties for {VariableName}", variable.Name);
                }
            }

            return result;
        }

        public override Task<StartProcessDataAnnouncementResponse> StartProcessDataAnnouncement(StartProcessDataAnnouncementRequest request, ServerCallContext context)
        {
            try
            {
                if (!_devices.TryGetValue(request.MasterId, out var device))
                {
                    _logger.LogError("Device {MasterId} not found for starting process data announcement", request.MasterId);
                    return Task.FromResult(new StartProcessDataAnnouncementResponse
                    {
                        ErrorCode = -1,
                        ErrorMessage = "Device not found",
                        IsStarted = false
                    });
                }

                // Get the HAL layer from the device
                var deviceHAL = GetDeviceHAL(device);
                if (deviceHAL == null)
                {
                    _logger.LogError("Could not access HAL layer for device {MasterId}", request.MasterId);
                    return Task.FromResult(new StartProcessDataAnnouncementResponse
                    {
                        ErrorCode = -2,
                        ErrorMessage = "Could not access device HAL layer",
                        IsStarted = false
                    });
                }

                deviceHAL.StartProcessDataAnnouncer();
                _logger.LogInformation("Started process data announcement for {MasterId}", request.MasterId);

                return Task.FromResult(new StartProcessDataAnnouncementResponse
                {
                    ErrorCode = 0,
                    ErrorMessage = "Process data announcement started successfully",
                    IsStarted = true
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting process data announcement for {MasterId}", request.MasterId);
                return Task.FromResult(new StartProcessDataAnnouncementResponse
                {
                    ErrorCode = -3,
                    ErrorMessage = $"Exception: {ex.Message}",
                    IsStarted = false
                });
            }
        }

        public override Task<StopProcessDataAnnouncementResponse> StopProcessDataAnnouncement(StopProcessDataAnnouncementRequest request, ServerCallContext context)
        {
            try
            {
                if (!_devices.TryGetValue(request.MasterId, out var device))
                {
                    _logger.LogError("Device {MasterId} not found for stopping process data announcement", request.MasterId);
                    return Task.FromResult(new StopProcessDataAnnouncementResponse
                    {
                        ErrorCode = -1,
                        ErrorMessage = "Device not found",
                        IsStopped = false
                    });
                }

                // Get the HAL layer from the device
                var deviceHAL = GetDeviceHAL(device);
                if (deviceHAL == null)
                {
                    _logger.LogError("Could not access HAL layer for device {MasterId}", request.MasterId);
                    return Task.FromResult(new StopProcessDataAnnouncementResponse
                    {
                        ErrorCode = -2,
                        ErrorMessage = "Could not access device HAL layer",
                        IsStopped = false
                    });
                }

                deviceHAL.StopProcessDataAnnouncer();
                _logger.LogInformation("Stopped process data announcement for {MasterId}", request.MasterId);

                return Task.FromResult(new StopProcessDataAnnouncementResponse
                {
                    ErrorCode = 0,
                    ErrorMessage = "Process data announcement stopped successfully",
                    IsStopped = true
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping process data announcement for {MasterId}", request.MasterId);
                return Task.FromResult(new StopProcessDataAnnouncementResponse
                {
                    ErrorCode = -3,
                    ErrorMessage = $"Exception: {ex.Message}",
                    IsStopped = false
                });
            }
        }

        private IMasterHAL? GetDeviceHAL(Device device)
        {
            try
            {
                // Access the private DeviceHAL field using reflection
                var deviceType = device.GetType();
                var halField = deviceType.GetField("DeviceHAL", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (halField != null)
                {
                    return halField.GetValue(device) as IMasterHAL;
                }

                var halProperty = deviceType.GetProperty("DeviceHAL", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (halProperty != null)
                {
                    return halProperty.GetValue(device) as IMasterHAL;
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get DeviceHAL via reflection");
                return null;
            }
        }

        private string GetEventName(ushort eventCode)
        {
            // Map IO-Link event codes to human-readable names
            // Based on IO-Link specification Table A.4
            return eventCode switch
            {
                0x1000 => "Device Appeared",
                0x1010 => "Device Disappeared",
                0x1020 => "Device Connected",
                0x1030 => "Device Disconnected",
                0x2000 => "Parameter Changed",
                0x3000 => "Process Data Invalid",
                0x4000 => "Communication Error",
                0x5000 => "Process Data Quality Alarm",
                0x6000 => "Device Status Changed",
                _ => $"Event_{eventCode:X4}"
            };
        }

        private string GetEventDescription(ushort eventCode, byte type)
        {
            // Provide detailed descriptions based on event code
            return eventCode switch
            {
                0x1000 => "A new IO-Link device has been detected on the port",
                0x1010 => "The IO-Link device is no longer available",
                0x1020 => "Connection to IO-Link device established successfully",
                0x1030 => "Connection to IO-Link device has been lost",
                0x2000 => "One or more device parameters have changed",
                0x3000 => "Process data from device is marked as invalid",
                0x4000 => "Communication error detected with IO-Link device",
                0x5000 => "Process data quality has degraded",
                0x6000 => "Device operating status has changed",
                _ => $"IO-Link event code {eventCode:X4}, type {type}"
            };
        }

        private string GetEventSeverity(byte type)
        {
            // Map event type to severity level
            // IO-Link event types: 0=Notification, 1=Warning, 2=Error
            return type switch
            {
                0 => "Info",
                1 => "Warning",
                2 => "Error",
                _ => "Unknown"
            };
        }
    }
}
