namespace OneDriver.Master.IoLink.Events
{
    public class ProcessDataEventArgs : EventArgs
    {
        public ProcessDataEventArgs(DeviceDescriptor.IoLink.Variables.Variable parameter, int channelNumber, DateTime timeStamp)
        {
            Parameter = parameter;
            ChannelNumber = channelNumber;
            TimeStamp = timeStamp;
        }

        public DeviceDescriptor.IoLink.Variables.Variable Parameter { get; }
        public int ChannelNumber { get; }
        public DateTime TimeStamp { get; }
    }
}
