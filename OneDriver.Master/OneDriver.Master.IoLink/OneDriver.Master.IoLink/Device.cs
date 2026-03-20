using DeviceDescriptor.Abstract;
using DeviceDescriptor.Abstract.Helper;
using DeviceDescriptor.Abstract.Variables;
using DeviceDescriptor.Factory;
using DeviceDescriptor.IoLink;
using DeviceDescriptor.IoLink.Source;
using DeviceDescriptor.IoLink.Variables;
using OneDriver.Framework.Base;
using OneDriver.Framework.Libs.Validator;
using OneDriver.Master.Abstract;
using OneDriver.Master.Abstract.Channels;
using OneDriver.Master.IoLink.Products;
using OneDriver.Module.Channel;
using OneDriver.Module.Parameter;
using Serilog;
using System.Buffers.Text;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net;
using static DeviceDescriptor.Abstract.Definition;

namespace OneDriver.Master.IoLink
{
    public class Device : CommonDevice<DeviceParams, Variable>
    {
        private IMasterHAL DeviceHAL { get; set; }
        Descriptor? DeviceDescriptor { get; set; }
        public Device(string name, IValidator validator, IMasterHAL deviceHAL) :
            base(new DeviceParams(name), validator,
                new ObservableCollection<BaseChannel<CommonVariables<Variable>>>()) 
        {
            DeviceHAL = deviceHAL;
            var request = new DescriptorRequest
            {
                Address = "c:\\temp\\F77.xml",
                DeviceId = "1116161",
                ProductName = "OQT150-R100-2EP-IO",
                IoLinkRevision = "1.1",
                ArticleNumber = "267075-100149",
            };
            DeviceDescriptor = DeviceDescriptorFactory.CreateIoLinkDescriptor(DescriptorType.LocalStorage, request);

            Init();
        }

        private void Init()
        {
            Parameters.PropertyChanging += Parameters_PropertyChanging;
            Parameters.PropertyChanged += Parameters_PropertyChanged;
            Parameters.PropertyReadRequested += Parameters_PropertyReadRequested;
            DeviceHAL.AttachToProcessDataEvent(ProcessDataChanged);

            for (var i = 0; i < DeviceHAL.NumberOfChannels; i++)
            {
                var channelParameters = new CommonVariables<BasicVariable>();
                var channel = new CommonChannel<Variable>(new CommonVariables<Variable>());
                Elements.Add(channel);
                Elements[i].Parameters.PropertyChanged += Parameters_PropertyChanged;
                Elements[i].Parameters.PropertyChanging += Parameters_PropertyChanging;
            }
        }

        private const int HashIndex = 253;

        private void Parameters_PropertyReadRequested(object sender, PropertyReadRequestedEventArgs e)
        {
            switch (e.PropertyName)
            {                
            }
        }

        private void ProcessDataChanged(object sender, InternalDataHAL e)
        {
            if (e.Data == null)
                return;

            var local = Elements[e.ChannelNumber].Parameters.PdInCollection.ToList().FindAll(x => x.Index == e.Index);
            foreach (var parameter in local)
            {
                var processValue = DataConverter.MaskByteArray(e.Data, parameter.Offset, parameter.LengthInBits,
                    parameter.DataType, true);
                TrySetVariableValue(parameter, processValue);
            }
        }

        private void Parameters_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(Parameters.SelectedChannel):
                    DeviceHAL.SensorPortNumber = Parameters.SelectedChannel;
                    break;
                case nameof(Parameters.Mode):
                    switch (Parameters.Mode)
                    {
                        case Abstract.Contracts.Definition.Mode.Communication:
                            DeviceHAL.StopProcessDataAnnouncer();
                            break;
                        case Abstract.Contracts.Definition.Mode.ProcessData:
                            DeviceHAL.StartProcessDataAnnouncer();
                            break;
                        case Abstract.Contracts.Definition.Mode.StandardInputOutput:
                            DeviceHAL.StopProcessDataAnnouncer();
                            break;
                    }
                    break;
            }
        }
        public Products.Definition.t_eInternal_Return_Codes AddProcessDataIndex(int processDataIndex) => DeviceHAL.SetProcessData((ushort)processDataIndex, out var length);
        

        private void Parameters_PropertyChanging(object sender, PropertyValidationEventArgs e)
        {
            //Write validity before property is changed here
            switch (e.PropertyName)
            {

            }
        }

        protected override int CloseConnection() => (int)DeviceHAL.Close();
        protected override int OpenConnection(string initString) => (int)DeviceHAL.Open(initString, Validator);

        public override int ConnectSensor()
        {
            var err = DeviceHAL.ConnectSensorWithMaster();
            Log.Information(err.ToString());

            return (err == Products.Definition.t_eInternal_Return_Codes.RETURN_OK) ? 0
                : (int)Abstract.Contracts.Definition.Error.SensorCommunicationError;
        }

        public override int DisconnectSensor() => (int)DeviceHAL.DisconnectSensorFromMaster();

        protected override string GetErrorAsText(int errorCode)
        {
            if (Enum.IsDefined(typeof(Products.Definition.t_eInternal_Return_Codes), errorCode))
                return ((Products.Definition.t_eInternal_Return_Codes)errorCode).ToString();

            return "UnknownError";
        }

        public int ReadParam(int index, int subindex, out byte[]? data)
        {
            var err = DeviceHAL.ReadRecord((ushort)index, (byte)subindex, out data, out _, out _, out _);
            if (data == null)
                throw new Exception("index: " + index + " read value is null");
            if (data.Length == 0)
                throw new Exception("index: " + index + " no data available");
            return (int)err;
        }
        public int WriteParam(int index, int subindex, byte[] data)
        {
            var err = DeviceHAL.WriteRecord((ushort)index, (byte)subindex, data, out _, out _);
            return (int)err;
        }
        protected override int ReadParam(Variable param)
        {
            TrySetVariableValue(param, null);
            var err = DeviceHAL.ReadRecord(Convert.ToUInt16(param.Index),
                Convert.ToByte(param.Subindex), out var data, out _, out _, out _);

            if (Equals(data, null))
                throw new Exception("index: " + param.Index + " read value is null");
            if (data.Length == 0)
                throw new Exception("index: " + param.Index + " no data available");


            if (param.DataType == DataType.UINT || param.DataType == DataType.INT || param.DataType == DataType.Float32 ||
                param.DataType == DataType.Byte || param.DataType == DataType.BOOL)
            {
                DataConverter.ToNumber(data, param.DataType, param.LengthInBits, true, out string?[] valueData);
                if (valueData == null)
                {
                    TrySetVariableValue(param, string.Join(";", data.Select(x => x.ToString()).ToArray()));
                    throw new Exception("index: " + param.Index + " data length mismatch");
                }

                TrySetVariableValue(param, string.Join(";", valueData));
            }

            if (param.DataType == DataType.CHAR)
            {
                DataConverter.ToString(data, out var val);
                TrySetVariableValue(param, val);
            }
            return (int)err;
        }

        public void LoadIodd(DescriptorRequest request, string baseUrl, string apiKey)
        {
            DeviceDescriptorFactory.ConfigureIoddFinder(baseUrl, apiKey);
            DeviceDescriptor = DeviceDescriptorFactory.CreateIoLinkDescriptor(DescriptorType.LocalStorage, request);

            if (DeviceDescriptor == null)
                throw new Exception("Failed to load IODD file: " );
            else
                this.Elements[this.Parameters.SelectedChannel].Parameters.ParamsCollection[0] 
                    = (Variable)DeviceDescriptor.Variables.ParamsCollection[0];

        }
        protected override int WriteParam(Variable param)
        {
            if (string.IsNullOrEmpty(param.Value))
                Log.Error(param.Name + " Data null");

            string[] dataToWrite = param.Value.Split(';').ToArray();
            DataConverter.DataError dataError;
            if ((dataError = DataConverter.ToByteArray(dataToWrite, param.DataType, param.LengthInBits,
                    true, out var returnedData, param.ArrayCount)) != DataConverter.DataError.NoError)
                return (int)dataError;
            return (int)DeviceHAL.WriteRecord((ushort)param.Index, (byte)param.Subindex, returnedData,
                out _, out _);
        }

        protected override int WriteCommand(Variable command) => WriteParam(command);

        
    }
}
