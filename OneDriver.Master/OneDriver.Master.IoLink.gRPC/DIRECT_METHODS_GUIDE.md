# Azure IoT Hub Direct Methods Guide

## ✅ What Changed?

Your gRPC service now supports **Azure IoT Hub Direct Methods** - a much better way to call methods directly from the cloud with **synchronous request/response**!

### Direct Methods vs C2D Messages:

| Feature | Direct Methods | C2D Messages |
|---------|---------------|--------------|
| **Communication** | Synchronous (request/response) | Asynchronous (fire & forget) |
| **Response** | Immediate return value | Telemetry sent separately |
| **Timeout** | Configurable (5-300 seconds) | No timeout |
| **Use Case** | Commands that need instant feedback | Long-running operations |
| **Azure Portal** | "Direct method" tab | "Message to device" tab |

---

## 🚀 How to Use Direct Methods from Azure Portal

### 1. Navigate to Direct Methods:
- **Azure Portal** → **IoT Hub** → **Devices** → **powersupply-001**
- Click **"Direct method"** tab

### 2. Call Methods:

#### **Method 1: ReadParameter**

**Method name:** `ReadParameter`

**Payload:**
```json
{
  "MasterId": "master-01",
  "ParameterName": "TN_V_SSP_SSC_SP1",
  "PortNumber": 0
}
```

**Response timeout:** 30 seconds  
**Connection timeout:** Device must already be connected

**Expected Response:**
```json
{
  "status": "success",
  "message": "Reading parameter TN_V_SSP_SSC_SP1"
}
```

The actual parameter value will be sent as telemetry to IoT Hub (same as before).

---

#### **Method 2: WriteParameter**

**Method name:** `WriteParameter`

**Payload:**
```json
{
  "MasterId": "master-01",
  "ParameterName": "TN_V_SSP_SSC_SP1",
  "Value": "100",
  "PortNumber": 0
}
```

**Expected Response:**
```json
{
  "status": "success",
  "message": "Writing parameter TN_V_SSP_SSC_SP1 = 100"
}
```

---

#### **Method 3: GetAllParameters** ⭐

**Method name:** `GetAllParameters`

**Payload:**
```json
{
  "MasterId": "master-01"
}
```

Or just empty `{}` (uses default master-01)

**Expected Response:**
```json
{
  "status": "success",
  "message": "Getting all parameters"
}
```

All parameters will be sent as individual telemetry messages to IoT Hub.

---

## 📱 Using Azure CLI

```bash
# Read Parameter
az iot hub invoke-device-method \
  --hub-name iot-powersupply-5678 \
  --device-id powersupply-001 \
  --method-name ReadParameter \
  --method-payload '{"MasterId":"master-01","ParameterName":"TN_V_SSP_SSC_SP1","PortNumber":0}'

# Write Parameter
az iot hub invoke-device-method \
  --hub-name iot-powersupply-5678 \
  --device-id powersupply-001 \
  --method-name WriteParameter \
  --method-payload '{"MasterId":"master-01","ParameterName":"TN_V_SSP_SSC_SP1","Value":"100","PortNumber":0}'

# Get All Parameters
az iot hub invoke-device-method \
  --hub-name iot-powersupply-5678 \
  --device-id powersupply-001 \
  --method-name GetAllParameters \
  --method-payload '{"MasterId":"master-01"}'
```

---

## 🔧 Using from C# Code (Backend)

If you're building a backend service (like OneDriver.Cloud.API), you can invoke direct methods programmatically:

```csharp
using Microsoft.Azure.Devices;

// Initialize ServiceClient (one time)
var serviceConnectionString = "YOUR_SERVICE_CONNECTION_STRING";
var serviceClient = ServiceClient.CreateFromConnectionString(serviceConnectionString);

// Invoke ReadParameter
var methodInvocation = new CloudToDeviceMethod("ReadParameter")
{
    ResponseTimeout = TimeSpan.FromSeconds(30)
};

var payload = new 
{
    MasterId = "master-01",
    ParameterName = "TN_V_SSP_SSC_SP1",
    PortNumber = 0
};

methodInvocation.SetPayloadJson(JsonSerializer.Serialize(payload));

var response = await serviceClient.InvokeDeviceMethodAsync("powersupply-001", methodInvocation);

Console.WriteLine($"Status: {response.Status}");
Console.WriteLine($"Response: {response.GetPayloadAsJson()}");
```

---

## 🎯 Supported Methods

Your device now supports these Direct Methods:

| Method Name | Parameters | Description |
|-------------|------------|-------------|
| **ReadParameter** | MasterId, ParameterName, PortNumber | Read single parameter value |
| **WriteParameter** | MasterId, ParameterName, Value, PortNumber | Write single parameter value |
| **GetAllParameters** | MasterId | Read all parameters from sensor |

Any other method name will return:
```json
{
  "status": "error",
  "message": "Method 'YourMethodName' not found"
}
```

---

## 📊 Viewing Results

### Direct Method Response:
You'll see the **immediate response** in Azure Portal's Direct Method result box.

### Parameter Values (Telemetry):
The actual parameter values are still sent as **telemetry** to IoT Hub. Monitor them using:

**Azure CLI:**
```bash
az iot hub monitor-events --hub-name iot-powersupply-5678
```

**Azure IoT Explorer:**
1. Open Azure IoT Explorer
2. Connect to your hub
3. Select device **powersupply-001**
4. Go to **Telemetry** tab
5. Click **Start**
6. Invoke Direct Method from another tab
7. See telemetry arrive in real-time!

---

## 🔄 Workflow Example

### Complete workflow for reading a parameter:

1. **Invoke Direct Method** from Azure Portal:
   - Method: `ReadParameter`
   - Payload: `{"MasterId":"master-01","ParameterName":"TN_V_SSP_SSC_SP1","PortNumber":0}`

2. **Get Immediate Response:**
   ```json
   {
     "status": "success",
     "message": "Reading parameter TN_V_SSP_SSC_SP1"
   }
   ```

3. **Device processes** the command and sends telemetry:
   ```json
   {
     "masterId": "master-01",
     "action": "readParameter",
     "parameterName": "TN_V_SSP_SSC_SP1",
     "value": "500",
     "errorCode": 0,
     "errorMessage": "Success",
     "timestamp": "2024-01-15T10:30:00Z"
   }
   ```

4. **Monitor telemetry** to see the actual value!

---

## 🆚 When to Use What?

### Use **Direct Methods** when:
- ✅ You need immediate confirmation that command was received
- ✅ You want synchronous request/response pattern
- ✅ You're building a backend API that needs to wait for results
- ✅ You want simpler error handling

### Use **C2D Messages** when:
- ✅ You want fire-and-forget messaging
- ✅ Device might be offline (queued delivery)
- ✅ Operation takes a long time (> 5 minutes)
- ✅ You don't need immediate response

---

## 🎉 Benefits

**Before (C2D Messages):**
```
You → Send C2D Message → Device → (wait) → Telemetry → You monitor telemetry
```

**After (Direct Methods):**
```
You → Call Direct Method → Device → Immediate Response ✅ + Telemetry
```

You get **both** instant confirmation AND detailed telemetry data!

---

## 🚨 Troubleshooting

**"Device not connected" error?**
- Make sure your gRPC service is running
- Check device connection in Azure Portal (should show "Connected")
- Verify Azure connection string in appsettings.json

**"Method timeout" error?**
- Increase response timeout (max 300 seconds)
- Check if sensor is connected at the specified port
- Look at gRPC service logs for errors

**No telemetry arriving?**
- Direct Method response is instant, telemetry comes separately
- Monitor telemetry using Azure CLI or IoT Explorer
- Check gRPC service logs to see if telemetry was sent

---

## 🎯 Quick Reference

### Azure Portal Steps:
1. **IoT Hub** → **Devices** → **powersupply-001**
2. Click **"Direct method"** tab
3. Enter method name: `GetAllParameters`
4. Enter payload: `{"MasterId":"master-01"}`
5. Click **"Invoke method"**
6. See instant response! ✅
7. Monitor telemetry separately for actual values

Your device is now controllable via **Direct Methods**! 🚀
