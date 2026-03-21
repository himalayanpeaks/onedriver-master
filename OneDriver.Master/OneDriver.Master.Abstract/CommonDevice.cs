using DeviceDescriptor.Abstract;
using DeviceDescriptor.Abstract.Helper;
using DeviceDescriptor.Abstract.Variables;
using OneDriver.Framework.Base;
using OneDriver.Framework.Libs.Validator;
using OneDriver.Master.Abstract.Channels;
using OneDriver.Master.Abstract.Contracts;
using OneDriver.Module.Channel;
using OneDriver.Module.Device;
using Serilog;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;

namespace OneDriver.Master.Abstract
{
    public abstract class CommonDevice<TDeviceParams, TChannelParams, TSensorVariable> :
        BaseDeviceWithChannels<CommonDeviceParams, TChannelParams>, IMaster
        where TDeviceParams : CommonDeviceParams
        where TChannelParams : CommonChannelParams
        where TSensorVariable : BasicVariable
    {
        protected BasicDescriptor<TSensorVariable> _descriptor { get; set; }
        protected CommonDevice(TDeviceParams parameters, IValidator validator,
           ObservableCollection<BaseChannel<TChannelParams>> elements, BasicDescriptor<TSensorVariable> descriptor)
           : base(parameters, validator, elements)
        {
            _descriptor = descriptor;
            Parameters.PropertyChanged += Parameters_PropertyChanged;
            Parameters.PropertyChanging += Parameters_PropertyChanging;
        }

        public string[] GetAllParamsFromSensor()
        {
            var channelParams = _descriptor.Variables;

            var allNames = new List<string>();

            if (_descriptor.Variables.ParamsCollection != null)
                allNames.AddRange(_descriptor.Variables.ParamsCollection.Where(x => !string.IsNullOrEmpty(x.Name)).Select(x => x.Name!));

            return allNames.ToArray();
        }

        /// <summary>
        /// Write here the validation of a param before its new value of a param is accepted 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// <exception cref="NotImplementedException"></exception>
        private void Parameters_PropertyChanging(object sender, PropertyValidationEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(Parameters.ProtocolId):
                    break;
            }
        }

        private void Parameters_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(Parameters.ProtocolId):
                    break;
            }
        }

        public Contracts.Definition.Error SelectSensorAtPort(int portNumber)
        {
            if (portNumber < Elements.Count)
                Parameters.SelectedChannel = portNumber;
            else
            {
                Log.Error(portNumber + " doesn't exist");
                return Contracts.Definition.Error.ChannelError;
            }
            return Contracts.Definition.Error.NoError;
        }

        public abstract int ConnectSensor();
        public abstract int DisconnectSensor();
        protected abstract int ReadParam(BasicVariable param);
        public Contracts.Definition.Error UpdateDataFromSensor()
        {
            if (Parameters.IsConnected == false)
            {
                Log.Error("Master not connected");
                return Contracts.Definition.Error.UptNotConnected;
            }

            foreach (var param in _descriptor.Variables.StandardVariables)
                ReadParameterFromSensor(param);
            foreach (var param in _descriptor.Variables.SpecificVariables)
                ReadParameterFromSensor(param);
            foreach (var param in _descriptor.Variables.SystemVariables)
                ReadParameterFromSensor(param);

            return Contracts.Definition.Error.NoError;
        }

        public void UpdateDataFromAllSensors()
        {
            if (this.Elements.Count > 1)
                if (Parameters.IsConnected)
                    DisconnectSensor();
            for (int i = 0; i < Elements.Count; i++)
            {
                if (Parameters.IsConnected == false)
                {
                    DisconnectSensor();
                    SelectSensorAtPort(i++);
                    ConnectSensor();
                }
                UpdateDataFromSensor();
            }
        }

        public int ReadParameterFromSensor(string name, out string? value)
        {
            BasicVariable? foundParam = FindParam(name);
            value = null;
            if (foundParam == null)
                return (int)Contracts.Definition.Error.ParameterNotFound;
            int err = ReadParameterFromSensor(foundParam);
            if (err == 0)
                value = foundParam.Value;
            return err;
        }

        public int ReadParameterFromSensor<T>(string name, out T? value)
        {
            value = default(T);
            int err = ReadParameterFromSensor(name, out var readValue);

            if (err == 0 && !string.IsNullOrEmpty(readValue) && DataConverter.ConvertTo(readValue, out value))
                return 0;
            return err != 0 ? err : (int)DataConverter.DataError.UnsupportedDataType;
        }
        private BasicVariable? FindCommand(string name) =>
            _descriptor.Variables.CommandsCollection.FirstOrDefault(x => x.Name == name);


        private BasicVariable? FindParam(string name)
        {
            BasicVariable? parameter = default;

            if (_descriptor.Variables.SpecificVariables != null)
                parameter = _descriptor.Variables.SpecificVariables
                    .FirstOrDefault(x => x.Name == name);

            if (parameter == null && _descriptor.Variables.SystemVariables != null)
                parameter = _descriptor.Variables.SystemVariables
                    .FirstOrDefault(x => x.Name == name);

            if (parameter == null && _descriptor.Variables.StandardVariables != null)
                parameter = _descriptor.Variables.StandardVariables
                    .FirstOrDefault(x => x.Name == name);

            return parameter;
        }

        public int WriteParameterToSensor(string name, string value)
        {
            if (DataConverter.ConvertTo<BasicVariable>(value, out var toWriteValue) == true)
                return WriteParameterToSensor(name, toWriteValue);
            return (int)DataConverter.DataError.InValidData;
        }

        public int WriteParameterToSensor<T>(string name, T value)
        {
            if (DataConverter.ConvertTo(value, out var toWriteValue))
                return WriteParameterToSensor(name, toWriteValue);
            return (int)DataConverter.DataError.InValidData;
        }

        public int WriteCommandToSensor(string name, string value)
        {
            BasicVariable? foundCommand = FindCommand(name);
            if (foundCommand == null)
            {
                Log.Error("Command not found: " + name);
                return (int)Contracts.Definition.Error.CommandNotFound;
            }

            if (!DataConverter.ConvertTo(value, out var toWriteValue))
            {
                Log.Error("Invalid data for command: " + name);
                return (int)DataConverter.DataError.InValidData;
            }

            if (!TrySetVariableValue(foundCommand, toWriteValue))
            {
                Log.Error("Unable to set command value: " + name);
                return (int)DataConverter.DataError.UnsupportedDataType;
            }

            return WriteCommandToSensor(foundCommand);
        }
        internal int WriteCommandToSensor(BasicVariable command)
        {
            int err = 0;
            try
            {
                if ((err = WriteCommand(command)) != 0)
                    Log.Error("Error in write command: " + GetErrorMessage(err));
            }
            catch (Exception e)
            {
                Log.Error(e.Message);
            }

            return err;
        }
        public int WriteCommandToSensor<T>(string name, T value)
        {
            if (DataConverter.ConvertTo<T>(value, out var toWriteValue) == true)
                return WriteCommandToSensor(name, toWriteValue);
            else
                return (int)DataConverter.DataError.InValidData;
        }
        protected override string GetErrorMessageFromDerived(int errorCode)
        {
            if (Enum.IsDefined(typeof(Contracts.Definition.Error), errorCode))
                return ((Contracts.Definition.Error)errorCode).ToString();
            if (Enum.IsDefined(typeof(DataConverter.DataError), errorCode))
                return ((DataConverter.DataError)errorCode).ToString();
            return GetErrorAsText(errorCode);
        }

        private int ReadParameterFromSensor(BasicVariable parameter)
        {
            int err = 0;
            try
            {
                if ((err = ReadParam(parameter)) != 0)
                    Log.Error("Error in read: " + GetErrorMessage(err));
            }
            catch (Exception e)
            {
                Log.Error(e.ToString());
            }
            return err;
        }

        protected static bool TrySetVariableValue(BasicVariable variable, string? value)
        {
            var setter = typeof(BasicVariable)
                .GetProperty(nameof(BasicVariable.Value), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.SetMethod;

            if (setter == null)
                return false;

            setter.Invoke(variable, [value]);
            return true;
        }


        protected abstract int WriteParam(BasicVariable param);
        protected abstract int WriteCommand(BasicVariable command);
        protected abstract string GetErrorAsText(int errorCode);
    }
}
