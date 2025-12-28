using Paspan.Fluent;
using System.Collections.Immutable;
using static Paspan.Fluent.Parsers;

namespace PaspanParsers.Json
{
    public class JsonParser
    {
        public static readonly Parser<IJson> Json;

        static JsonParser()
        {
            var LBrace = Terms.Char('{');
            var RBrace = Terms.Char('}');
            var LBracket = Terms.Char('[');
            var RBracket = Terms.Char(']');
            var Colon = Terms.Char(':');
            var Comma = Terms.Char(',');

            var String = Terms.String(StringLiteralQuotes.Double);

            var jsonString =
                String
                    .Then<IJson>(static s => new JsonString(s));

            var jsonNumber =
                Terms.Decimal()
                    .Then<IJson>(static d => new JsonNumber(d));

            var jsonTrue =
                Terms.Text("true")
                    .Then<IJson>(static _ => new JsonBoolean(true));

            var jsonFalse =
                Terms.Text("false")
                    .Then<IJson>(static _ => new JsonBoolean(false));

            var jsonNull =
                Terms.Text("null")
                    .Then<IJson>(static _ => JsonNull.Instance);

            var json = Deferred<IJson>();

            var jsonArray =
                Between(LBracket, Separated(Comma, json), RBracket)
                    .Then<IJson>(static els => new JsonArray(els.ToImmutableArray()));

            var jsonMember =
                String.Skip(Colon).And(json)
                    .Then(static member => new KeyValuePair<string, IJson>(member.Item1, member.Item2));

            var jsonObject =
                Between(LBrace, Separated(Comma, jsonMember), RBrace)
                    .Then<IJson>(static kvps => new JsonObject(kvps.ToImmutableDictionary()));

            Json = json.Parser = jsonString.Or(jsonNumber).Or(jsonTrue).Or(jsonFalse).Or(jsonNull).Or(jsonArray).Or(jsonObject);
        }

        public static IJson Parse(string input)
        {
            if (Json.TryParse(input, out var result))
            {
                return result;
            }
            else
            {
                return null;
            }
        }

        public static IJson Parse(byte[] input)
        {
            if (Json.TryParse(input, out var result))
            {
                return result;
            }
            else
            {
                return null;
            }
        }
    }
}
