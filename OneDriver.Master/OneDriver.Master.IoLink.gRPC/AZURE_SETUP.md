# Azure IoT Hub Integration Setup

This guide explains how to connect your IO-Link master to Azure IoT Hub for cloud-based parameter reading and writing.

## Prerequisites

1. **Azure IoT Hub**: Create an IoT Hub in Azure Portal
2. **Device Registration**: Register a device in your IoT Hub
3. **NuGet Package**: Install the Azure IoT SDK

## Step 1: Install Azure IoT Hub SDK

Run this command in the project directory:

```bash
cd OneDriver.Master.IoLink.gRPC
dotnet add package Microsoft.Azure.Devices.Client
```

## Step 2: Configure appsettings.json

Your `appsettings.json` is already configured with:

```json
{
  "IODDFinder": {
    "BaseUrl": "https://ioddfinder.io-link.com",
    "ApiKey": "c4ca4238-a0b9-2382-0dcc-509a6f75849b"
  },
  "AzureIoTHub": {
    "ConnectionString": "HostName=iot-powersupply-5678.azure-devices.net;DeviceId=powersupply-001;SharedAccessKey=..."
  },
  "IoLinkMaster": {
    "DefaultMasterId": "master-01",
    "ComPort": "COM3",
    "AutoConnectOnStartup": true,
    "Descriptor": {
      "DeviceId": "3147528",
      "ProductName": "UB6000-F42-2EP-IO-V15",
      "IoLinkRevision": "1.1",
      "ArticleNumber": "70165295-100021"
    }
  }
}
```

**Update the Azure IoT Hub connection string** with your actual device connection string from Azure Portal.

## Step 3: Enable Azure IoT Hub in AzureIoTHubService.cs

After installing the NuGet package, open `Services/AzureIoTHubService.cs` and **uncomment** the following lines:

### In the constructor (line ~21):
```csharp
// Uncomment this:
_deviceClient = DeviceClient.CreateFromConnectionString(connectionString, TransportType.Mqtt);
```

### In SendTelemetryAsync method (line ~55-61):
```csharp
// Uncomment this:
var message = new Message(Encoding.UTF8.GetBytes(messageString))
{
    ContentType = "application/json",
    ContentEncoding = "utf-8"
};
await ((DeviceClient)_deviceClient).SendEventAsync(message);
```

### In SendProcessDataAsync method (line ~93-99):
```csharp
// Uncomment this:
var message = new Message(Encoding.UTF8.GetBytes(messageString))
{
    ContentType = "application/json",
    ContentEncoding = "utf-8"
};
await ((DeviceClient)_deviceClient).SendEventAsync(message);
```

### Also add using statement at the top:
```csharp
using Microsoft.Azure.Devices.Client;
```

## Step 4: Run the Service

```bash
dotnet run --project OneDriver.Master.IoLink.gRPC
```

On startup, the service will:
1. Configure IODD Finder with your API credentials
2. Create the device descriptor for UB6000-F42-2EP-IO-V15
3. Connect to the IO-Link master at COM3
4. Automatically send telemetry to Azure IoT Hub when parameters are read

## How It Works

### Auto-Connection on Startup
The `IoLinkMasterHostedService` automatically:
- Configures IODD Finder
- Creates the device descriptor with your specified device parameters
- Connects to COM3
- Registers the device with the gRPC service

### Telemetry to Azure IoT Hub
When you call the `ReadParameter` gRPC method, the service:
1. Reads the parameter from the IO-Link sensor
2. Automatically sends the data to Azure IoT Hub

**Telemetry Format:**
```json
{
  "masterId": "master-01",
  "portNumber": 1,
  "parameterName": "Temperature",
  "value": "25.5",
  "timestamp": "2024-01-15T10:30:00Z"
}
```

## Reading Parameters from Cloud

From your cloud application, you can now:

1. **Call the gRPC service** to read parameters:
   ```csharp
   var response = await client.ReadParameterAsync(new ReadParameterRequest
   {
       MasterId = "master-01",
       ParameterName = "VendorName",
       PortNumber = 0
   });
   ```

2. **View telemetry in Azure IoT Hub** using:
   - Azure IoT Explorer
   - Azure Stream Analytics
   - Azure Functions with IoT Hub triggers
   - Azure Monitor

## Writing Parameters from Cloud

To write parameters from the cloud:

1. **Use Azure IoT Hub Device Methods** or **Direct Methods**
2. **Or call the gRPC service** directly:
   ```csharp
   var response = await client.WriteParameterAsync(new WriteParameterRequest
   {
       MasterId = "master-01",
       ParameterName = "Setpoint",
       Value = "100",
       PortNumber = 0
   });
   ```

## Monitoring

View logs to confirm:
- ✅ IODD Finder configured
- ✅ Descriptor created
- ✅ Master connected at COM3
- ✅ Telemetry sent to Azure IoT Hub

## Next Steps

1. Install the Azure IoT SDK package
2. Uncomment the code in `AzureIoTHubService.cs`
3. Update your Azure IoT Hub connection string
4. Run the service
5. Monitor telemetry in Azure Portal

Your IO-Link data will now flow to the cloud! 🚀
