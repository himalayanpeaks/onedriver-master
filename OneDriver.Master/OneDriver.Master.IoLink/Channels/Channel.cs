using OneDriver.Master.Abstract.Channels;

namespace OneDriver.Master.IoLink.Channels
{
    public class Channel : CommonChannel<ChannelParams>
    {
        public Channel(ChannelParams parameters) : base(parameters)
        {
        }
    }
}
