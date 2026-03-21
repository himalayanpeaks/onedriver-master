using OneDriver.Module.Parameter;

namespace OneDriver.Master.Abstract.Channels
{
    public class CommonChannelParams(string name) : BaseChannelParams(name)
    {
        private bool isActive;
        public bool IsActive
        {
            get => GetProperty(ref isActive);
            set => SetProperty(ref isActive, value);
        }
    }
}
