using Paspan;
using System.Collections.Immutable;

namespace PaspanParsers.Json
{
    public interface IJson
    {
    }

    public class JsonArray(ImmutableArray<IJson> elements) : IJson
    {
        public ImmutableArray<IJson> Elements { get; } = elements;

        public override string ToString()
            => $"[{string.Join(",", Elements.Select(e => e.ToString()))}]";
    }

    public class JsonArrayRegion(ImmutableArray<IJson> elements) : IJson
    {
        public ImmutableArray<IJson> Elements { get; } = elements;

        public string ToString(ReadOnlySpan<byte> data)
        {
            var list = new List<string>();
            foreach (var el in Elements)
            {
                switch (el)
                {
                    case JsonStringRegion s:
                        list.Add(s.ToString(data));
                        break;
                    case JsonObjectRegion o:
                        list.Add($"\"{o.ToString(data)}");
                        break;
                    case JsonArrayRegion a:
                        list.Add($"\"{a.ToString(data)}");
                        break;
                    default:
                        throw new NotImplementedException();

                };
            }
            return $"[{string.Join(",", list)}]";
        }
    }

    public class JsonObject(IImmutableDictionary<string, IJson> members) : IJson
    {
        public IImmutableDictionary<string, IJson> Members { get; } = members;

        public override string ToString()
            => $"{{{string.Join(",", Members.Select(kvp => $"\"{kvp.Key}\":{kvp.Value}"))}}}";
    }

    public class JsonObjectRegion(IImmutableDictionary<Region, IJson> members) : IJson
    {
        public IImmutableDictionary<Region, IJson> Members { get; } = members;

        public override string ToString()
           => $"{{{string.Join(",", Members.Select(kvp => $"\"{kvp.Key}\":{kvp.Value}"))}}}";
        public string ToString(ReadOnlySpan<byte> data)
        {
            var list = new List<string>();
            foreach (var kvp in Members)
            {
                switch (kvp.Value)
                {
                    //case Region r:
                    //    list.Add($"\"{kvp.Key.ToString(data)}\":{r.ToString(data)}");
                    //    break;
                    case JsonStringRegion s:
                        list.Add($"\"{kvp.Key.ToString(data)}\":{s.ToString(data)}");
                        break;
                    case JsonObjectRegion o:
                        list.Add($"\"{kvp.Key.ToString(data)}\":{o.ToString(data)}");
                        break;
                    case JsonArrayRegion a:
                        list.Add($"\"{kvp.Key.ToString(data)}\":{a.ToString(data)}");
                        break;
                    default:
                        throw new NotImplementedException();

                };
            }
            return $"{{{string.Join(",", list)}}}";
        }
    }

    public class JsonString(string value) : IJson
    {
        public string Value { get; } = value;

        public override string ToString()
            => $"\"{Value}\"";
    }

    public class JsonStringRegion(Region value) : IJson
    {
        public Region Value { get; } = value;

        public string ToString(ReadOnlySpan<byte> data)
            => $"\"{Value.ToString(data)}\"";
    }

    public class JsonNumber(decimal value) : IJson
    {
        public decimal Value { get; } = value;

        public override string ToString()
            => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    public class JsonBoolean(bool value) : IJson
    {
        public bool Value { get; } = value;

        public override string ToString()
            => Value ? "true" : "false";
    }

    public class JsonNull : IJson
    {
        public static readonly JsonNull Instance = new();

        private JsonNull() { }

        public override string ToString()
            => "null";
    }

}
