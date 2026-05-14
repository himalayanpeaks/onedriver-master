# ✅ Direct Methods Now Return Actual Values!

## What Changed?

Direct Methods now return **actual parameter values** in the response, not just status messages!

---

## 🚀 Usage Examples

### **1. ReadParameter** - Get Immediate Value

**Method name:** `ReadParameter`

**Payload:**
```json
{
  "MasterId": "master-01",
  "ParameterName": "TN_V_SSP_SSC_SP1",
  "PortNumber": 0
}
```

**Response (NEW!):**
```json
{
  "status": 200,
  "payload": {
    "status": "success",
    "parameterName": "TN_V_SSP_SSC_SP1",
    "value": "500",
    "dataType": "UIntegerT",
    "errorCode": 0,
    "errorMessage": "Success"
  }
}
```

✅ You get the **actual value "500"** immediately in the response!

---

### **2. WriteParameter** - Confirm Write

**Method name:** `WriteParameter`

**Payload:**
```json
{
  "MasterId": "master-01",
  "ParameterName": "STD_TN_V_ProductID",
  "Value": "12345-66000",
  "PortNumber": 0
}
```

**Response:**
```json
{
  "status": 200,
  "payload": {
    "status": "success",
    "parameterName": "STD_TN_V_ProductID",
    "value": "12345-66000",
    "message": "Parameter written successfully"
  }
}
```

---

### **3. GetAllParameters** - Get All Values at Once

**Method name:** `GetAllParameters`

**Payload:**
```json
{
  "MasterId": "master-01"
}
```

**Response:**
```json
{
  "status": 200,
  "payload": {
    "status": "success",
    "count": 45,
    "parameters": [
      {
        "name": "TN_V_SSP_SSC_SP1",
        "value": "500",
        "dataType": "UIntegerT"
      },
      {
        "name": "TN_V_SSP_SSC_SP2",
        "value": "250",
        "dataType": "UIntegerT"
      },
      {
        "name": "TN_V_SSP_SSC01_Param.TN_V_SSP_SSC_SP3",
        "value": "100",
        "dataType": "UIntegerT"
      }
      // ... all 45 parameters with their actual values!
    ]
  }
}
```

✅ You get **ALL parameter values** in a single response!

---

## 📱 Using from Azure Portal

### Step-by-Step:

1. **Azure Portal** → **IoT Hub** → **Devices** → **powersupply-001**
2. Click **"Direct method"** tab
3. **Method name:** `ReadParameter`
4. **Payload:**
   ```json
   {
     "MasterId": "master-01",
     "ParameterName": "TN_V_SSP_SSC_SP1",
     "PortNumber": 0
   }
   ```
5. **Response timeout:** 30 seconds
6. Click **"Invoke method"**
7. **See the actual value** in the Result box! ✨

---

## 🔧 Using from C# Backend

```csharp
using Microsoft.Azure.Devices;
using System.Text.Json;

var serviceClient = ServiceClient.CreateFromConnectionString("YOUR_SERVICE_CONNECTION_STRING");

// Read parameter and get value immediately
var method = new CloudToDeviceMethod("ReadParameter")
{
    ResponseTimeout = TimeSpan.FromSeconds(30)
};

method.SetPayloadJson(JsonSerializer.Serialize(new
{
    MasterId = "master-01",
    ParameterName = "TN_V_SSP_SSC_SP1",
    PortNumber = 0
}));

var response = await serviceClient.InvokeDeviceMethodAsync("powersupply-001", method);

if (response.Status == 200)
{
    var result = JsonSerializer.Deserialize<DirectMethodResponse>(response.GetPayloadAsJson());
    Console.WriteLine($"Parameter value: {result.Value}");
    Console.WriteLine($"Data type: {result.DataType}");
}

public class DirectMethodResponse
{
    public string Status { get; set; }
    public string ParameterName { get; set; }
    public string Value { get; set; }
    public string DataType { get; set; }
    public int ErrorCode { get; set; }
    public string ErrorMessage { get; set; }
}
```

---

## 🆚 Before vs After

### ❌ Before (Just a message):
```json
{
  "status": "success",
  "message": "Reading parameter TN_V_SSP_SSC_SP1"
}
```
You had to wait for telemetry to see the actual value.

### ✅ After (Actual value included):
```json
{
  "status": "success",
  "parameterName": "TN_V_SSP_SSC_SP1",
  "value": "500",
  "dataType": "UIntegerT",
  "errorCode": 0,
  "errorMessage": "Success"
}
```
You get the value **immediately** in the response!

---

## 📊 Data Still Sent as Telemetry

**Bonus:** The parameter values are ALSO sent as telemetry to IoT Hub, so you can:
- ✅ Monitor in real-time with Azure CLI or IoT Explorer
- ✅ Store in Azure Stream Analytics / Time Series Insights
- ✅ Build historical dashboards

You get **both** synchronous responses AND telemetry streams!

---

## 🎯 Benefits

| Feature | Value |
|---------|-------|
| **Immediate Results** | Get parameter value in < 1 second |
| **No Polling** | Direct response, no waiting for telemetry |
| **Type Information** | Know if it's UIntegerT, Float, String, etc. |
| **Error Handling** | Instant feedback if something fails |
| **Batch Operations** | GetAllParameters returns all values at once |

---

## 🚨 Error Responses

If parameter doesn't exist:
```json
{
  "status": 400,
  "payload": {
    "status": "error",
    "parameterName": "INVALID_PARAM",
    "errorCode": 5,
    "message": "Parameter not found"
  }
}
```

If device is offline:
```json
{
  "status": 404,
  "payload": {
    "error": "DeviceNotOnline"
  }
}
```

---

## 🔄 Complete Workflow Example

### Scenario: Read sensor temperature from cloud

1. **Call Direct Method** from your backend:
```csharp
var method = new CloudToDeviceMethod("ReadParameter");
method.SetPayloadJson("{\"ParameterName\":\"TN_V_SSP_SSC_SP1\"}");
var response = await serviceClient.InvokeDeviceMethodAsync("powersupply-001", method);
```

2. **Get immediate response** (< 1 second):
```json
{
  "value": "500",
  "dataType": "UIntegerT"
}
```

3. **Use the value** in your application:
```csharp
var result = JsonSerializer.Deserialize<DirectMethodResponse>(response.GetPayloadAsJson());
if (result.ErrorCode == 0)
{
    int temperature = int.Parse(result.Value);
    Console.WriteLine($"Current temperature: {temperature}°C");

    // Make decisions based on value
    if (temperature > 100)
    {
        await SendAlert("Temperature too high!");
    }
}
```

4. **Historical data** also available via telemetry stream for charts and analytics.

---

## 🎉 Try It Now!

**Restart your gRPC service** and invoke `ReadParameter` from Azure Portal.

You'll see the actual parameter value in the response! 🚀

### Quick Test:
```
Method: ReadParameter
Payload: {"MasterId":"master-01","ParameterName":"TN_V_SSP_SSC_SP1","PortNumber":0}
Expected: {"status":"success","value":"500",...}
```

Perfect for building real-time dashboards and control systems! ✨
