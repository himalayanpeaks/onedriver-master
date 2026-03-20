namespace OneDriver.Master.Abstract.Contracts
{
    public class Definition
    {
        public enum Error
        {
            NoError = 0,
            ChannelError = int.MinValue,
            ParameterNotFound = int.MinValue + 1,
            DatabaseNotConnected = int.MinValue + 2,
            SensorCommunicationError = int.MinValue + 3,
            CommandNotFound = int.MinValue + 4,
            SensorNotFound = int.MinValue + 5,
            UptNotConnected = int.MinValue + 6,
            HasdIdNotFound = int.MinValue + 7,
        }

        public enum Mode
        {
            Communication,
            ProcessData,
            StandardInputOutput
        }

    }
}
