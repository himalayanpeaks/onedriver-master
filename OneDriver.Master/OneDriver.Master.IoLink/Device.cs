using DeviceDescriptor.Abstract.Helper;
using DeviceDescriptor.Abstract.Variables;
using DeviceDescriptor.IoLink;
using DeviceDescriptor.IoLink.Variables;
using OneDriver.Framework.Base;
using OneDriver.Framework.Libs.Validator;
using OneDriver.Master.Abstract;
using OneDriver.Master.IoLink.Channels;
using OneDriver.Master.IoLink.Products;
using OneDriver.Module.Channel;
using Serilog;
using System.Collections.ObjectModel;
using System.ComponentModel;
using static DeviceDescriptor.Abstract.Definition;

namespace OneDriver.Master.IoLink
{
    public class Device : CommonDevice<DeviceParams, ChannelParams, Variable>
    {
        private IMasterHAL DeviceHAL { get; set; }

        public Device(string name, IValidator validator, IMasterHAL deviceHAL, Descriptor descriptor) :
            base(new DeviceParams(name), validator,
                new ObservableCollection<BaseChannel<ChannelParams>>(), descriptor)
        {
            DeviceHAL = deviceHAL;
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
                var channel = new Channels.Channel(new ChannelParams(""));
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

            var local = _descriptor.Variables.PdInCollection.ToList().FindAll(x => x.Index == e.Index);
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
        protected override int ReadParam(BasicVariable param)
        {
            TrySetVariableValue(param, null);
            var err = DeviceHAL.ReadRecord(Convert.ToUInt16(param.Index),
                Convert.ToByte(((Variable)param).Subindex), out var data, out _, out _, out _);

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
            else if (param.DataType == DataType.CHAR)
            {
                DataConverter.ToString(data, out var val);
                TrySetVariableValue(param, val);
            }
            else
            {
                // For RecordT and other unsupported types, return raw bytes as comma-separated values
                TrySetVariableValue(param, string.Join(",", data.Select(x => x.ToString()).ToArray()));
            }
            return (int)err;
        }

        protected override int WriteParam(BasicVariable param)
        {
            if (string.IsNullOrEmpty(param.Value))
                Log.Error(param.Name + " Data null");

            string[] dataToWrite = param.Value.Split(';').ToArray();
            DataConverter.DataError dataError;
            if ((dataError = DataConverter.ToByteArray(dataToWrite, param.DataType, param.LengthInBits,
                    true, out var returnedData, param.ArrayCount)) != DataConverter.DataError.NoError)
                return (int)dataError;
            return (int)DeviceHAL.WriteRecord((ushort)param.Index, (byte)((Variable)param).Subindex, returnedData,
                out _, out _);
        }

        protected override int WriteCommand(BasicVariable command) => WriteParam(command);


    }
}
