using OneDriver.Framework.Libs.Announcer;

namespace OneDriver.Master.IoLink.Products
{
    public class InternalDataHAL : BaseDataForAnnouncement
    {
        public InternalDataHAL(int channelNumber, byte[] data)
        {
            TimeStamp = DateTime.Now;
            ChannelNumber = channelNumber;
            Data = data;
        }

        public InternalDataHAL()
        {
            TimeStamp = DateTime.Now;
            Data = new byte[0];
            ChannelNumber = 0;
        }

        public int ChannelNumber { get; } = 0;
        public byte[] Data { get; }
    }
}
