using OneDriver.Module.Parameter;

namespace OneDriver.Master.Abstract
{
    public class CommonDeviceParams : BaseDeviceParams
    {
        private int protocolId;
        private Contracts.Definition.Mode _mode;

        public int ProtocolId
        {
            get => GetProperty(ref protocolId);
            set => SetProperty(ref protocolId, value);
        }
        public Contracts.Definition.Mode Mode
        {
            get => _mode;
            set => SetProperty(ref _mode, value);
        }
        private int _selectedChannel;

        public int SelectedChannel
        {
            get => _selectedChannel;
            internal set => SetProperty(ref _selectedChannel, value);
        }
        public CommonDeviceParams(string name) : base(name)
        {
        }
    }
}
