using OneDriver.Framework.Libs.Announcer;

namespace OneDriver.Master.IoLink.Products
{
    public class InternalDataHAL : BaseDataForAnnouncement
    {
        public InternalDataHAL(int channelNumber, byte[] processdata, ushort number, ushort eventCode, byte instance, byte mode, byte type, byte pdValid, byte localGenerated, uint sensorStatus)
        {
            TimeStamp = DateTime.Now;
            ChannelNumber = channelNumber;
            ProcessData = processdata;
            Number = number;
            EventCode = eventCode;
            Instance = instance;
            Mode = mode;
            Type = type;
            PdValid = pdValid;
            LocalGenerated = localGenerated;
            SensorStatus = sensorStatus;
        }

        public InternalDataHAL()
        {
            TimeStamp = DateTime.Now;
            ProcessData = new byte[0];
            ChannelNumber = 0;
        }

        public int ChannelNumber { get; } = 0;
        public byte[] ProcessData { get; }
        public ushort Number { get; }
        public ushort EventCode { get; }
        public byte Instance { get; }
        public byte Mode { get; }
        public byte Type { get; }
        public byte PdValid { get; }
        public byte LocalGenerated { get; }
        public uint SensorStatus { get; }
    }
}
