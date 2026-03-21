using DeviceDescriptor.IoLink;
using OneDriver.Framework.Libs.Validator;
using OneDriver.Master.Abstract.Contracts;
using OneDriver.Master.IoLink;
using OneDriver.Master.IoLink.Products;

namespace OneDriver.Master.Factory
{
    public enum MasterType
    {
        TmgMaster2 = 0,
    }
    public class MasterFactory
    {
        public static IMaster? CreateCommonMaster(MasterType masterType, IMasterHAL deviceHAL, Descriptor descriptor, IValidator? validator = null)
        {
            validator ??= new ComportValidator();

            switch (masterType)
            {
                case MasterType.TmgMaster2:
                    return new Device("master", validator, deviceHAL, descriptor);
            }
            return null;
        }

        public static Device? CreateIoLinkMaster(MasterType masterType, IMasterHAL deviceHAL, Descriptor descriptor, IValidator? validator = null)
        {
            validator ??= new Framework.Libs.Validator.ComportValidator();

            switch (masterType)
            {
                case MasterType.TmgMaster2:
                    return new Device("master", validator, deviceHAL, descriptor);
            }
            return null;
        }
    }
}
