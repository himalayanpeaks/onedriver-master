using DeviceDescriptor.Abstract.Variables;
using DeviceDescriptor.IoLink.Source;
using OneDriver.Master.Abstract;
using OneDriver.Master.IoLink.Products;
using Microsoft.Extensions.Configuration;
using OneDriver.Master.IoLink;
using DeviceDescriptor.IoLink.Variables;
using DeviceDescriptor.Factory;
using OneDriver.Master.Abstract.Contracts;

namespace OneDriver.Master.Factory
{
    public enum MasterType
    {
        TmgMaster2 = 0,        
    }
    public class MasterFactory
    {
        public static IMaster? CreateCommonMaster(MasterType masterType)
        {

            switch (masterType)
            {
                case MasterType.TmgMaster2:
                    return new Device("master", new Framework.Libs.Validator.ComportValidator(), new TmgMaster2());
            }
            return null;
        }

        public static Device? CreateIoLinkMaster(MasterType masterType)
        {
            switch (masterType)
            {
                case MasterType.TmgMaster2:
                    return new Device("master", new Framework.Libs.Validator.ComportValidator(), new TmgMaster2());
            }
            return null;
        }
    }
}
