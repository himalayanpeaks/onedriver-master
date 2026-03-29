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

        public override Task<AddProcessDataIndexResponse> AddProcessDataIndex(AddProcessDataIndexRequest request, ServerCallContext context)
        {
            try
            {
                if (!_devices.TryGetValue(request.MasterId, out var device))
                {
                    return Task.FromResult(new AddProcessDataIndexResponse
                    {
                        ErrorCode = -1,
                        ErrorMessage = "Device not found"
                    });
                }

                var result = device.AddProcessDataIndex(request.ProcessDataIndex);
                return Task.FromResult(new AddProcessDataIndexResponse
                {
                    ErrorCode = (int)result,
                    ErrorMessage = result.ToString(),
                    LengthInBytes = 0 // You may need to capture this from AddProcessDataIndex
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding process data index {Index}", request.ProcessDataIndex);
                return Task.FromResult(new AddProcessDataIndexResponse
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
                    ProcessDataCount = descriptor.Variables.PdInCollection.Count
                };

                foreach (var param in descriptor.Variables.ParamsCollection)
                {
                    response.Parameters.Add(MapVariableToProto(param));
                }

                foreach (var cmd in descriptor.Variables.CommandsCollection)
                {
                    response.Commands.Add(MapVariableToProto(cmd));
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

        private VariableData MapVariableToProto(DeviceDescriptor.IoLink.Variables.Variable variable)
        {
            return new VariableData
            {
                Name = variable.Name ?? string.Empty,
                Index = variable.Index,
                Subindex = variable.Subindex,
                Value = variable.Value ?? string.Empty,
                DataType = variable.DataType.ToString(),
                LengthInBits = variable.LengthInBits,
                Offset = variable.Offset,
                IsDynamic = variable.IsDynamic,
                ArrayCount = variable.ArrayCount
            };
        }
    }
}
