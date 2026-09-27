using Paspan.Fluent;
using static Paspan.Fluent.Parsers;

namespace PaspanParsers.CSharp;

// Lexical level of the combinator grammar: punctuation and keywords of declarations.
// Tokens of types, expressions and statements are scanned by SyntaxParser.
public partial class CSharpParser
{
    // Punctuation
    private static Parser<string> COMMA, DOT, SEMICOLON, COLON, LPAREN, RPAREN, LBRACE, RBRACE,
        LBRACKET, RBRACKET, EQ, ARROW;

    // Keywords
    private static Parser<string> NAMESPACE, USING, CLASS, STRUCT, INTERFACE, ENUM;

    // Modifiers
    private static Parser<string> PUBLIC, PRIVATE, PROTECTED, INTERNAL, STATIC, READONLY, CONST, VIRTUAL,
        OVERRIDE, ABSTRACT, SEALED, PARTIAL, ASYNC, EXTERN, UNSAFE, VOLATILE, NEW, REQUIRED, REF;

    // Accessor keywords
    private static Parser<string> GET, SET, INIT;

    // Global attribute targets
    private static Parser<string> ATTR_ASSEMBLY, ATTR_MODULE;

    /// <summary>
    /// A reserved or contextual keyword, preceded by trivia.
    /// </summary>
    private static Parser<string> Keyword(string text) => SkipWhiteSpace(new KeywordToken(text));

    /// <summary>
    /// An operator or punctuator, preceded by trivia, that is not the start of a longer one.
    /// </summary>
    private static Parser<string> Punct(string text) => SkipWhiteSpace(new PunctuatorToken(text));

    private static void InitializeLexical()
    {
        COMMA = Punct(",");
        DOT = Punct(".");
        SEMICOLON = Punct(";");
        COLON = Punct(":");
        LPAREN = Punct("(");
        RPAREN = Punct(")");
        LBRACE = Punct("{");
        RBRACE = Punct("}");
        LBRACKET = Punct("[");
        RBRACKET = Punct("]");
        EQ = Punct("=");
        ARROW = Punct("=>");

        NAMESPACE = Keyword("namespace");
        USING = Keyword("using");
        CLASS = Keyword("class");
        STRUCT = Keyword("struct");
        INTERFACE = Keyword("interface");
        ENUM = Keyword("enum");

        PUBLIC = Keyword("public");
        PRIVATE = Keyword("private");
        PROTECTED = Keyword("protected");
        INTERNAL = Keyword("internal");
        STATIC = Keyword("static");
        READONLY = Keyword("readonly");
        CONST = Keyword("const");
        VIRTUAL = Keyword("virtual");
        OVERRIDE = Keyword("override");
        ABSTRACT = Keyword("abstract");
        SEALED = Keyword("sealed");
        PARTIAL = Keyword("partial");
        ASYNC = Keyword("async");
        EXTERN = Keyword("extern");
        UNSAFE = Keyword("unsafe");
        VOLATILE = Keyword("volatile");
        NEW = Keyword("new");
        REQUIRED = Keyword("required");
        REF = Keyword("ref");

        GET = Keyword("get");
        SET = Keyword("set");
        INIT = Keyword("init");

        ATTR_ASSEMBLY = Keyword("assembly");
        ATTR_MODULE = Keyword("module");
    }
}
