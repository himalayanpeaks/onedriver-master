using DeviceDescriptor.Abstract.Variables;
using OneDriver.Module.Channel;
using OneDriver.Module.Parameter;
using System.Security.Cryptography.X509Certificates;

namespace OneDriver.Master.Abstract.Channels
{
    public class CommonChannel<TChannelParams> : BaseChannel<CommonVariables<TChannelParams>>
        where TChannelParams : BasicVariable
    {
        private bool isActive;

        public CommonChannel(CommonVariables<TChannelParams> parameters) : base(parameters)
        {
        }

        public bool IsActive
        {
            get => GetProperty(ref isActive);
            set => SetProperty(ref isActive, value);
        }
    }
}
