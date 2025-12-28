using Paspan;
using Paspan.Fluent;
using System.Collections.Immutable;
using static Paspan.Fluent.Parsers;

namespace PaspanParsers.Json
{
    public class JsonParserRegion
    {
        public static readonly Parser<IJson> Json;

        static JsonParserRegion()
        {
            var LBrace = Terms.Char('{');
            var RBrace = Terms.Char('}');
            var LBracket = Terms.Char('[');
            var RBracket = Terms.Char(']');
            var Colon = Terms.Char(':');
            var Comma = Terms.Char(',');

            var String = Literals.StringRegion(StringLiteralQuotes.Double);

            var jsonString =
                String
                    .Then<IJson>(static s => new JsonStringRegion(s));

            var json = Deferred<IJson>();

            var jsonArray =
                Between(LBracket, Separated(Comma, json), RBracket)
                    .Then<IJson>(static els => new JsonArrayRegion([.. els]));

            var jsonMember =
                String.Skip(Colon).And(json)
                    .Then(static member => new KeyValuePair<Region, IJson>(member.Item1, member.Item2));

            var jsonObject =
                Between(LBrace, Separated(Comma, jsonMember), RBrace)
                    .Then<IJson>(static kvps => new JsonObjectRegion(kvps.ToImmutableDictionary()));

            Json = json.Parser = jsonString.Or(jsonArray).Or(jsonObject);
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
