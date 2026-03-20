using OneDriver.Master.Abstract.Contracts;

namespace OneDriver.Master.Abstract.UnitTest
{
    public class IMasterStructureTests
    {
        [Fact]
        public void IMaster_Interface_ShouldContainExpectedMethodNames()
        {
            var expected = new[]
            {
                "SelectSensorAtPort",
                "ConnectSensor",
                "DisconnectSensor",
                "UpdateDataFromSensor",
                "UpdateDataFromAllSensors",
                "ReadParameterFromSensor",
                "WriteParameterToSensor",
                "WriteCommandToSensor",
                "GetErrorMessage",
                "GetAllParamsFromSensor",
            };

            var actual = typeof(IMaster).GetMethods()
                           .Select(m => m.Name)
                           .ToHashSet();

            foreach (var name in expected)
                Assert.Contains(name, actual);
        }
    }
}
