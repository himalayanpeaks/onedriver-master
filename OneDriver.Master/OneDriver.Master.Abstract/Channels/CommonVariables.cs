using DeviceDescriptor.Abstract.Variables;
using OneDriver.Module.Parameter;
using System;
using System.Collections.Generic;
using System.Text;

namespace OneDriver.Master.Abstract.Channels
{
    public class CommonVariables<TSensorVariable> : DeviceVariables<TSensorVariable>, IChannelParams

        where TSensorVariable : BasicVariable
    {
        public string Name { get; init; }
    }
}
