using OneDriver.Framework.Base;
using OneDriver.Framework.Libs.Validator;
using OneDriver.Module;
using OneDriver.Module.Parameter;
using Serilog;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using OneDriver.Master.Abstract.Contracts;
using DeviceDescriptor.Abstract.Variables;
using DeviceDescriptor.Abstract.Helper;
using OneDriver.Module.Device;
using OneDriver.Module.Channel;
using OneDriver.Master.Abstract.Channels;

namespace OneDriver.Master.Abstract
{
    public abstract class CommonDevice<TDeviceParams, TSensorVariable> :
        BaseDeviceWithChannels<TDeviceParams, CommonVariables<TSensorVariable>>, IMaster
        where TDeviceParams : CommonDeviceParams
        where TSensorVariable : BasicVariable
    {
        protected CommonDevice(TDeviceParams parameters, IValidator validator,
           ObservableCollection<BaseChannel<CommonVariables<TSensorVariable>>> elements)
           : base(parameters, validator, elements)
        {
            Parameters.PropertyChanged += Parameters_PropertyChanged;
            Parameters.PropertyChanging += Parameters_PropertyChanging;
        }

        public string[] GetAllParamsFromSensor()
        {
            var channelParams = Elements[Parameters.SelectedChannel].Parameters;

            var allNames = new List<string>();

            if (channelParams.ParamsCollection != null)
                allNames.AddRange(channelParams.ParamsCollection.Where(x => !string.IsNullOrEmpty(x.Name)).Select(x => x.Name!));

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
        protected abstract int ReadParam(TSensorVariable param);
        public Contracts.Definition.Error UpdateDataFromSensor()
        {
            if (Parameters.IsConnected == false)
            {
                Log.Error("Master not connected");
                return Contracts.Definition.Error.UptNotConnected;
            }

            foreach (var param in Elements[Parameters.SelectedChannel].Parameters.StandardVariables)
                ReadParameterFromSensor(param);
            foreach (var param in Elements[Parameters.SelectedChannel].Parameters.SpecificVariables)
                ReadParameterFromSensor(param);
            foreach (var param in Elements[Parameters.SelectedChannel].Parameters.SystemVariables)
                ReadParameterFromSensor(param);

            return Contracts.Definition.Error.NoError;
        }

        public void UpdateDataFromAllSensors()
        {
            if (this.Elements.Count > 1)
                if (Parameters.IsConnected)
                    DisconnectSensor();
            for(int i = 0; i < Elements.Count; i++)
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
            TSensorVariable? foundParam = FindParam(name);
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
        private TSensorVariable? FindCommand(string name) => 
            Elements[Parameters.SelectedChannel].Parameters.CommandsCollection.FirstOrDefault(x => x.Name == name);


        private TSensorVariable? FindParam(string name)
        {
            TSensorVariable? parameter = default;

            if (Elements[Parameters.SelectedChannel].Parameters.SpecificVariables != null)
                parameter = Elements[Parameters.SelectedChannel].Parameters.SpecificVariables
                    .FirstOrDefault(x => x.Name == name);

            if (parameter == null && Elements[Parameters.SelectedChannel].Parameters.SystemVariables != null)
                parameter = Elements[Parameters.SelectedChannel].Parameters.SystemVariables
                    .FirstOrDefault(x => x.Name == name);

            if (parameter == null && Elements[Parameters.SelectedChannel].Parameters.StandardVariables != null)
                parameter = Elements[Parameters.SelectedChannel].Parameters.StandardVariables
                    .FirstOrDefault(x => x.Name == name);

            return parameter;
        }

        public int WriteParameterToSensor(string name, string value)
        {
            if (DataConverter.ConvertTo<TSensorVariable>(value, out var toWriteValue) == true)
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
            TSensorVariable? foundCommand = FindCommand(name);
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
        internal int WriteCommandToSensor(TSensorVariable command)
        {
            int err = 0;
            try
            {
                if((err = WriteCommand(command)) !=0)
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
        
        private int ReadParameterFromSensor(TSensorVariable parameter)
        {
            int err = 0;
            try
            {
                if((err = ReadParam(parameter)) != 0)
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

        
        protected abstract int WriteParam(TSensorVariable param);
        protected abstract int WriteCommand(TSensorVariable command);
        protected abstract string GetErrorAsText(int errorCode);
    }
}
