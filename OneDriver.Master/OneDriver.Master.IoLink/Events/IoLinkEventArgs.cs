using DeviceDescriptor.IoLink.Variables;

namespace OneDriver.Master.IoLink.Events
{
    /// <summary>
    /// Event arguments for IO-Link device events
    /// </summary>
    public class IoLinkEventArgs : EventArgs
    {
        public IoLinkEventArgs(
            ushort eventNumber,
            ushort eventCode,
            byte instance,
            byte mode,
            byte type,
            byte pdValid,
            byte localGenerated,
            uint sensorStatus,
            int channelNumber,
            DateTime timeStamp)
        {
            EventNumber = eventNumber;
            EventCode = eventCode;
            Instance = instance;
            Mode = mode;
            Type = type;
            PdValid = pdValid;
            LocalGenerated = localGenerated;
            SensorStatus = sensorStatus;
            ChannelNumber = channelNumber;
            TimeStamp = timeStamp;
        }

        /// <summary>
        /// Sequential event number from the device
        /// </summary>
        public ushort EventNumber { get; }

        /// <summary>
        /// IO-Link event code (see IO-Link specification)
        /// </summary>
        public ushort EventCode { get; }

        /// <summary>
        /// Event instance identifier
        /// </summary>
        public byte Instance { get; }

        /// <summary>
        /// Operating mode when event occurred
        /// </summary>
        public byte Mode { get; }

        /// <summary>
        /// Event type (e.g., Notification, Warning, Error)
        /// </summary>
        public byte Type { get; }

        /// <summary>
        /// Process data validity flag
        /// </summary>
        public byte PdValid { get; }

        /// <summary>
        /// Indicates if event was locally generated (vs. from device)
        /// </summary>
        public byte LocalGenerated { get; }

        /// <summary>
        /// Channel number where event occurred
        /// </summary>
        public int ChannelNumber { get; }

        /// <summary>
        /// Timestamp when event was received
        /// </summary>
        public DateTime TimeStamp { get; }

        /// <summary>
        /// Sensor status information
        /// </summary>
        public uint SensorStatus { get; }
    }
}
