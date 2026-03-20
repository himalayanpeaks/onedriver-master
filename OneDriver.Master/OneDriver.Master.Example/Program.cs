using DeviceDescriptor.IoLink.Source;
using OneDriver.Master.Factory;
using OneDriver.Master.IoLink;
using Serilog;

namespace OneDriver.Master.Example
{
    class Program
    {
        static void Main(string[] args)
        {
            ConfigureLogging();

            var master = MasterFactory.CreateCommonMaster(MasterType.TmgMaster2);
            ((IoLink.Device)master).LoadIodd(new DescriptorRequest
            {
                Address = "c:\\temp\\F77.xml",
                DeviceId = "",
                ProductName = "",
                IoLinkRevision = "",
                ArticleNumber = "",
            }, "https://example.com/iodd/", "your_api_key_here");
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
