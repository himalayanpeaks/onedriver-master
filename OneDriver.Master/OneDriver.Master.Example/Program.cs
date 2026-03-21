using DeviceDescriptor.Factory;
using DeviceDescriptor.IoLink.Source;
using Microsoft.Extensions.Configuration;
using OneDriver.Master.Factory;
using OneDriver.Master.IoLink;
using OneDriver.Master.IoLink.Products;
using Serilog;

namespace OneDriver.Master.Example
{
    class Program
    {
        static void Main(string[] args)
        {
            ConfigureLogging();

            var config = new ConfigurationBuilder().AddJsonFile("appsettings.local.json", optional: false).Build();
            string baseUrl = config["IODDFinder:Test:BaseUrl"]!;
            string apiKey = config["IODDFinder:Test:ApiKey"]!;

            Log.Information("--- Application configures descriptor ---");
            DeviceDescriptorFactory.ConfigureIoddFinder(baseUrl, apiKey);

            var descriptorRequest = new DescriptorRequest
            {
                Address = "c:\\temp\\F77.xml",
                DeviceId = "1116161",
                ProductName = "OQT150-R100-2EP-IO",
                IoLinkRevision = "1.1",
                ArticleNumber = "267075-100149",
            };

            var descriptor = DeviceDescriptorFactory.CreateIoLinkDescriptor(DescriptorType.LocalStorage, descriptorRequest);
            Log.Information($"Descriptor created with {descriptor.Variables.ParamsCollection.Count} parameters");

            Log.Information("--- Application creates HAL ---");
            var deviceHAL = new TmgMaster2();

            Log.Information("--- Factory creates Master with application-provided objects ---");
            var master = MasterFactory.CreateCommonMaster(MasterType.TmgMaster2, deviceHAL, descriptor);
            if (master == null)
            {
                Log.Error("Failed to create master");
                return;
            }

            Log.Information("Master created successfully");

            var connectResult = master.Connect("COM3");
            if (connectResult != 0)
            {
                Log.Error($"Failed to connect to master: {master.GetErrorMessage(connectResult)}");
                return;
            }

            Log.Information("Connected to master");

            var selectResult = master.SelectSensorAtPort(0);
            if (selectResult != OneDriver.Master.Abstract.Contracts.Definition.Error.NoError)
            {
                Log.Error($"Failed to select sensor at port 0");
                master.Disconnect();
                return;
            }

            var sensorConnectResult = master.ConnectSensor();
            if (sensorConnectResult != 0)
            {
                Log.Error($"Failed to connect to sensor: {master.GetErrorMessage(sensorConnectResult)}");
                master.Disconnect();
                return;
            }

            Log.Information("Connected to sensor at port 0");

            if (master is Device ioLinkDevice)
            {
                Log.Information("--- Accessing Descriptor (IoLink-Specific) ---");
                // var desc = ioLinkDevice._descriptor;
                Log.Information($"Descriptor has {descriptor.Variables.ParamsCollection.Count} parameters and {descriptor.Variables.CommandsCollection.Count} commands");

                var firstParam = descriptor.Variables.ParamsCollection.FirstOrDefault();
                if (firstParam != null)
                {
                    Log.Information($"First Parameter: Name={firstParam.Name}, Index={firstParam.Index}, Subindex={firstParam.Subindex}");
                }

                Log.Information("--- Accessing Channel Variables (IoLink-Specific) ---");
                var channel = ioLinkDevice.Elements[0];
                var channelVars = descriptor.Variables.ParamsCollection.Take(5);
                foreach (var variable in channelVars)
                {
                    Log.Information($"Channel Variable: {variable.Name}, Index={variable.Index}, Subindex={variable.Subindex}");
                }
            }

            var readResult = master.ReadParameterFromSensor("VendorName", out string? vendorName);
            if (readResult == 0 && vendorName != null)
            {
                Log.Information($"Vendor Name: {vendorName}");
            }
            else
            {
                Log.Error($"Failed to read VendorName: {master.GetErrorMessage(readResult)}");
            }

            master.DisconnectSensor();
            master.Disconnect();

            Log.Information("Disconnected successfully");
        }

        static void ConfigureLogging()
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .CreateLogger();
        }
    }
}
