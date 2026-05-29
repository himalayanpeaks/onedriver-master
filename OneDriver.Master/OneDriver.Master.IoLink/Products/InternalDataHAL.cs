using OneDriver.Framework.Libs.Announcer;

namespace OneDriver.Master.IoLink.Products
{
    public class InternalDataHAL : BaseDataForAnnouncement
    {
        public InternalDataHAL(int channelNumber, byte[] processdata)
        {
            TimeStamp = DateTime.Now;
            ChannelNumber = channelNumber;
            ProcessData = processdata;
        }

        public InternalDataHAL()
        {
            TimeStamp = DateTime.Now;
            ProcessData = new byte[0];
            ChannelNumber = 0;
        }

        public int ChannelNumber { get; } = 0;
        public byte[] ProcessData { get; }
    }
}
