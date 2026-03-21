using OneDriver.Module.Channel;

namespace OneDriver.Master.Abstract.Channels
{
    public class CommonChannel<TChannelParams> : BaseChannel<TChannelParams>
        where TChannelParams : CommonChannelParams
    {

        public CommonChannel(TChannelParams parameters) : base(parameters)
        {
        }

    }
}
