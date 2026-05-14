# Reading Data from Console Client

You have **3 ways** to read data from your gRPC service:

---

## Option 1: Direct gRPC Console Client ✅ (Recommended)

I've created a **OneDriver.Master.ConsoleClient** project for you that connects directly to your gRPC service.

### How to Use:

1. **Start your gRPC service** first:
   ```bash
   cd OneDriver.Master.IoLink.gRPC
   dotnet run
   ```

2. **Run the console client**:
   ```bash
   cd OneDriver.Master.ConsoleClient
   dotnet run
   ```

3. **Interactive Menu:**
   - **Option 1**: Get All Parameters (lists all available parameters with values)
   - **Option 2**: Read Specific Parameter (read one parameter by name)
   - **Option 3**: Write Parameter (change a parameter value)
   - **Option 4**: Monitor Parameter (continuous reading every X seconds)

### Example Session:
```
=== IO-Link Master gRPC Console Client ===

Choose an option:
1. Get All Parameters
2. Read Specific Parameter
3. Write Parameter
4. Monitor Parameter (continuous)
5. Exit

Your choice: 1

📋 Fetching all parameters...

✅ Found 45 parameters:

  📌 TN_V_SSP_SSC_SP1
     Value: 500
     Type: UIntegerT
     Access: ReadWrite

  📌 TN_V_SSP_SSC_SP2
     Value: 250
     Type: UIntegerT
     Access: ReadWrite
```

---

## Option 2: Azure Cloud-to-Device Message 🌐

Send commands from **Azure Portal** and read telemetry responses.

### Messages to Send:

#### **Get All Parameters:**
```json
{
  "Action": "getAllParameters",
  "MasterId": "master-01",
  "PortNumber": 0
}
```

Or shorter:
```json
{
  "Action": "getall",
  "MasterId": "master-01"
}
```

#### **Read Specific Parameter:**
```json
{
  "Action": "read",
  "MasterId": "master-01",
  "ParameterName": "TN_V_SSP_SSC_SP1",
  "PortNumber": 0
}
```

#### **Write Parameter:**
```json
{
  "Action": "write",
  "MasterId": "master-01",
  "ParameterName": "TN_V_SSP_SSC_SP1",
  "Value": "100",
  "PortNumber": 0
}
```

### How to Send:

**From Azure Portal:**
1. Go to **Azure Portal** → **IoT Hub** → **Devices**
2. Click on **powersupply-001**
3. Click **"Message to device"**
4. Paste JSON message
5. Click **"Send Message"**

**From Azure CLI:**
```bash
# Get all parameters
az iot device c2d-message send \
  --hub-name iot-powersupply-5678 \
  --device-id powersupply-001 \
  --data '{"Action":"getall","MasterId":"master-01"}'

# Read specific parameter
az iot device c2d-message send \
  --hub-name iot-powersupply-5678 \
  --device-id powersupply-001 \
  --data '{"Action":"read","MasterId":"master-01","ParameterName":"TN_V_SSP_SSC_SP1","PortNumber":0}'
```

### View Telemetry Responses:

**Option A: Azure CLI**
```bash
az iot hub monitor-events --hub-name iot-powersupply-5678
```

**Option B: Azure IoT Explorer** (Recommended)
1. Download: https://github.com/Azure/azure-iot-explorer/releases
2. Connect to your IoT Hub
3. Select device **powersupply-001**
4. Go to **"Telemetry"** tab → Click **"Start"**
5. Send C2D command
6. See real-time telemetry stream!

### Expected Telemetry for GetAll:

After sending `getAllParameters`, you'll receive **multiple telemetry messages**:

```json
{
  "masterId": "master-01",
  "portNumber": 0,
  "parameterName": "TN_V_SSP_SSC_SP1",
  "value": "500",
  "timestamp": "2024-01-15T10:30:00Z"
}
```

```json
{
  "masterId": "master-01",
  "portNumber": 0,
  "parameterName": "TN_V_SSP_SSC_SP2",
  "value": "250",
  "timestamp": "2024-01-15T10:30:01Z"
}
```

...and a summary:
```json
{
  "masterId": "master-01",
  "action": "getAllParameters",
  "parameterName": "AllParameters",
  "value": "45 parameters sent",
  "errorCode": 0,
  "errorMessage": "Successfully retrieved 45 parameters",
  "timestamp": "2024-01-15T10:30:05Z"
}
```

---

## Option 3: Azure IoT Hub Telemetry Listener (C# Console)

Create a console app that listens to Azure IoT Hub Event Hub endpoint.

### Create Project:
```bash
dotnet new console -n OneDriver.Cloud.TelemetryListener
cd OneDriver.Cloud.TelemetryListener
dotnet add package Azure.Messaging.EventHubs
dotnet add package Azure.Messaging.EventHubs.Processor
dotnet add package Azure.Storage.Blobs
```

### Program.cs:
```csharp
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Consumer;
using System.Text;
using System.Text.Json;

var eventHubConnectionString = "YOUR_EVENT_HUB_COMPATIBLE_ENDPOINT";
var consumerGroup = EventHubConsumerClient.DefaultConsumerGroupName;

await using var consumer = new EventHubConsumerClient(consumerGroup, eventHubConnectionString);

Console.WriteLine("🔊 Listening for telemetry from Azure IoT Hub...\n");

await foreach (PartitionEvent partitionEvent in consumer.ReadEventsAsync())
{
    if (partitionEvent.Data == null) continue;

    var messageBody = Encoding.UTF8.GetString(partitionEvent.Data.EventBody.ToArray());

    try
    {
        var telemetry = JsonSerializer.Deserialize<JsonDocument>(messageBody);
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Telemetry received:");
        Console.WriteLine(messageBody);
        Console.WriteLine();
    }
    catch
    {
        Console.WriteLine($"Raw message: {messageBody}");
    }
}
```

Get `eventHubConnectionString` from:
- Azure Portal → IoT Hub → **Built-in endpoints** → **Event Hub-compatible endpoint**

---

## Summary

| Method | Use Case | Real-time | Complexity |
|--------|----------|-----------|------------|
| **gRPC Console Client** | Local development, testing | ✅ Yes | ⭐ Easy |
| **Azure C2D Messages** | Remote control from cloud | ✅ Yes | ⭐⭐ Medium |
| **Azure Telemetry Listener** | Monitor cloud data stream | ✅ Yes | ⭐⭐⭐ Advanced |

**Recommended workflow:**
1. Use **gRPC Console Client** for local testing
2. Use **Azure C2D + Telemetry** for cloud integration
3. Build **OneDriver.Cloud** solution for web/mobile dashboards

---

## Quick Start Commands

```bash
# Terminal 1: Start gRPC Service
cd OneDriver.Master.IoLink.gRPC
dotnet run

# Terminal 2: Run Console Client
cd OneDriver.Master.ConsoleClient
dotnet run

# Or send from Azure Portal:
# Message to device → {"Action":"getall","MasterId":"master-01"}
```

Your device is now controllable locally and from the cloud! 🚀
