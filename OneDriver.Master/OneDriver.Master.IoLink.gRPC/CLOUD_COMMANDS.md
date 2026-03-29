# Sending Commands from Azure IoT Hub to Read/Write Parameters

## Quick Start

After installing the Azure IoT SDK and running your service, you can send commands from Azure Portal to control your IO-Link device.

## Message Format for Azure Portal

### To Read Parameter: TN_V_SSP_SSC_SP1

In the **"Message to device"** section in Azure Portal, send:

```json
{
  "Action": "readParameter",
  "MasterId": "master-01",
  "ParameterName": "TN_V_SSP_SSC_SP1",
  "PortNumber": 0
}
```

### To Write Parameter

```json
{
  "Action": "writeParameter",
  "MasterId": "master-01",
  "ParameterName": "TN_V_SSP_SSC_SP1",
  "Value": "100",
  "PortNumber": 0
}
```

### To Send Command

```json
{
  "Action": "writeCommand",
  "MasterId": "master-01",
  "ParameterName": "Reset",
  "Value": "1",
  "PortNumber": 0
}
```

## How It Works

1. **You send a message** from Azure Portal (or any cloud app)
2. **Device receives** the Cloud-to-Device (C2D) message
3. **Service processes** the command and reads/writes the parameter from IO-Link sensor
4. **Service sends result** back to Azure IoT Hub as telemetry
5. **You see the result** in Azure IoT Hub telemetry stream

## Expected Response in Azure Telemetry

After sending the read command, you'll see:

```json
{
  "masterId": "master-01",
  "action": "readParameter",
  "parameterName": "TN_V_SSP_SSC_SP1",
  "value": "25.5",
  "errorCode": 0,
  "errorMessage": "Success",
  "timestamp": "2024-01-15T10:30:00Z"
}
```

## Step-by-Step in Azure Portal

### 1. Navigate to Your Device
- Go to Azure Portal → IoT Hub → Devices
- Click on **powersupply-001**

### 2. Send Message to Device
- Click **"Message to device"**
- In **Message Body**, paste:
  ```json
  {
    "Action": "read",
    "ParameterName": "TN_V_SSP_SSC_SP1",
    "PortNumber": 0
  }
  ```
- Click **"Send Message"**

### 3. View the Response
- Go to **"Device twin"** or use **Azure IoT Explorer**
- Monitor telemetry messages
- You'll see the parameter value in the telemetry stream

## Using Azure IoT Explorer (Recommended)

1. Download [Azure IoT Explorer](https://github.com/Azure/azure-iot-explorer/releases)
2. Connect to your IoT Hub
3. Select device **powersupply-001**
4. Go to **"Cloud-to-device message"** tab
5. Send the JSON command
6. Go to **"Telemetry"** tab and click **"Start"**
7. See the response immediately!

## Using Azure CLI

```bash
# Read parameter
az iot device c2d-message send \
  --hub-name iot-powersupply-5678 \
  --device-id powersupply-001 \
  --data '{"Action":"read","ParameterName":"TN_V_SSP_SSC_SP1","PortNumber":0}'

# Write parameter
az iot device c2d-message send \
  --hub-name iot-powersupply-5678 \
  --device-id powersupply-001 \
  --data '{"Action":"write","ParameterName":"TN_V_SSP_SSC_SP1","Value":"100","PortNumber":0}'
```

## Monitoring Commands

Watch your gRPC service logs to see:
- Received C2D messages
- Parameter read/write operations
- Responses sent back to Azure

## Troubleshooting

**Not receiving messages?**
- Ensure you've installed the Azure IoT SDK package
- Verify you've uncommented the code in `AzureIoTHubService.cs`
- Check your connection string is correct
- Look at service logs for errors

**Parameter not found?**
- Verify the parameter name matches exactly (case-sensitive)
- Check available parameters using `GetAllParameters` gRPC method
- Ensure sensor is connected at the port

Your device is now controllable from the cloud! 🌐
