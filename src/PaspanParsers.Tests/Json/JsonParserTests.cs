using System.Text;
using PaspanParsers.Json;

namespace PaspanParsers.Tests.Json
{
    [TestClass]
    public class JsonParserTests
    {
        [TestMethod]
        [DataRow("{\"property\":\"value\"}")]
        [DataRow("{\"property\":[\"value\",\"value\",\"value\"]}")]
        [DataRow("{\"property\":{\"property\":\"value\"}}")]
        public void ShouldParseJson(string json)
        {
            var result = JsonParser.Parse(json);
            Assert.AreEqual(json, result.ToString());
        }

        [TestMethod]
        [DataRow("{\"property\":\"value\"}")]
        [DataRow("{\"property\":[\"value\",\"value\",\"value\"]}")]
        [DataRow("{\"property\":{\"property\":\"value\"}}")]
        public void ShouldParseRegionJson(string json)
        {
            var data = Encoding.UTF8.GetBytes(json);
            var result = JsonParserRegion.Parse(data);
            Assert.AreEqual(json, ((JsonObjectRegion)result).ToString(data));
        }

    }
}
