using Paspan;
using Paspan.Fluent;
using static Paspan.Fluent.Parsers;

namespace PaspanParsers.CSharp;

// Lexical level: punctuation, keywords and literals.
public partial class CSharpParser
{
    // Punctuation
    private static Parser<string> COMMA, DOT, SEMICOLON, COLON, LPAREN, RPAREN, LBRACE, RBRACE,
        LBRACKET, RBRACKET, LT, GT, EQ, QUESTION, ARROW;

    // Keywords
    private static Parser<string> NAMESPACE, USING, CLASS, STRUCT, INTERFACE, ENUM, RECORD, DELEGATE;

    // Modifiers
    private static Parser<string> PUBLIC, PRIVATE, PROTECTED, INTERNAL, STATIC, READONLY, CONST, VIRTUAL,
        OVERRIDE, ABSTRACT, SEALED, PARTIAL, ASYNC, EXTERN, UNSAFE, VOLATILE, NEW, REQUIRED,
        REF, OUT, IN, PARAMS;

    // Statement keywords
    private static Parser<string> IF, ELSE, WHILE, DO, FOR, FOREACH, SWITCH, CASE, DEFAULT, BREAK,
        CONTINUE, RETURN, THROW, TRY, CATCH, FINALLY, LOCK, YIELD, AWAIT;

    // Other keywords
    private static Parser<string> VAR, VOID, GET, SET, INIT, ADD, REMOVE, WHERE, THIS, BASE, OPERATOR,
        IMPLICIT, EXPLICIT, AS, IS, TYPEOF, SIZEOF, NAMEOF, WHEN, AND, OR, NOT;

    // Literal keywords
    private static Parser<string> TRUE, FALSE, NULL;

    // Predefined type keywords
    private static Parser<string> OBJECT, STRING, BOOL, BYTE, SBYTE, SHORT, USHORT, INT, UINT, LONG,
        ULONG, FLOAT, DOUBLE, DECIMAL, CHAR, DYNAMIC;

    // Attribute target keywords
    private static Parser<string> ATTR_ASSEMBLY, ATTR_MODULE, ATTR_FIELD, ATTR_EVENT, ATTR_METHOD,
        ATTR_PARAM, ATTR_PROPERTY, ATTR_RETURN, ATTR_TYPE;

    // Query keywords
    private static Parser<string> FROM, SELECT, WHERE_KW, LET, JOIN, ON, EQUALS, INTO, ORDERBY,
        ASCENDING, DESCENDING, GROUP, BY;

    // Literals
    private static Parser<Expression> numericLiteral, stringLiteral, charLiteral, interpolatedString,
        boolLiteral, nullLiteral, literal;

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
        LT = Punct("<");
        // '>' closing a type argument list may be followed by another '>' or by '=' (List<List<int>>)
        GT = SkipWhiteSpace(new PunctuatorToken(">", maximalMunch: false));
        EQ = Punct("=");
        QUESTION = Punct("?");
        ARROW = Punct("=>");

        NAMESPACE = Keyword("namespace");
        USING = Keyword("using");
        CLASS = Keyword("class");
        STRUCT = Keyword("struct");
        INTERFACE = Keyword("interface");
        ENUM = Keyword("enum");
        RECORD = Keyword("record");
        DELEGATE = Keyword("delegate");

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
        OUT = Keyword("out");
        IN = Keyword("in");
        PARAMS = Keyword("params");

        IF = Keyword("if");
        ELSE = Keyword("else");
        WHILE = Keyword("while");
        DO = Keyword("do");
        FOR = Keyword("for");
        FOREACH = Keyword("foreach");
        SWITCH = Keyword("switch");
        CASE = Keyword("case");
        DEFAULT = Keyword("default");
        BREAK = Keyword("break");
        CONTINUE = Keyword("continue");
        RETURN = Keyword("return");
        THROW = Keyword("throw");
        TRY = Keyword("try");
        CATCH = Keyword("catch");
        FINALLY = Keyword("finally");
        LOCK = Keyword("lock");
        YIELD = Keyword("yield");
        AWAIT = Keyword("await");

        VAR = Keyword("var");
        VOID = Keyword("void");
        GET = Keyword("get");
        SET = Keyword("set");
        INIT = Keyword("init");
        ADD = Keyword("add");
        REMOVE = Keyword("remove");
        WHERE = Keyword("where");
        THIS = Keyword("this");
        BASE = Keyword("base");
        OPERATOR = Keyword("operator");
        IMPLICIT = Keyword("implicit");
        EXPLICIT = Keyword("explicit");
        AS = Keyword("as");
        IS = Keyword("is");
        TYPEOF = Keyword("typeof");
        SIZEOF = Keyword("sizeof");
        NAMEOF = Keyword("nameof");
        WHEN = Keyword("when");
        AND = Keyword("and");
        OR = Keyword("or");
        NOT = Keyword("not");

        TRUE = Keyword("true");
        FALSE = Keyword("false");
        NULL = Keyword("null");

        OBJECT = Keyword("object");
        STRING = Keyword("string");
        BOOL = Keyword("bool");
        BYTE = Keyword("byte");
        SBYTE = Keyword("sbyte");
        SHORT = Keyword("short");
        USHORT = Keyword("ushort");
        INT = Keyword("int");
        UINT = Keyword("uint");
        LONG = Keyword("long");
        ULONG = Keyword("ulong");
        FLOAT = Keyword("float");
        DOUBLE = Keyword("double");
        DECIMAL = Keyword("decimal");
        CHAR = Keyword("char");
        DYNAMIC = Keyword("dynamic");

        ATTR_ASSEMBLY = Keyword("assembly");
        ATTR_MODULE = Keyword("module");
        ATTR_FIELD = Keyword("field");
        ATTR_EVENT = Keyword("event");
        ATTR_METHOD = Keyword("method");
        ATTR_PARAM = Keyword("param");
        ATTR_PROPERTY = Keyword("property");
        ATTR_RETURN = Keyword("return");
        ATTR_TYPE = Keyword("type");

        FROM = Keyword("from");
        SELECT = Keyword("select");
        WHERE_KW = Keyword("where");
        LET = Keyword("let");
        JOIN = Keyword("join");
        ON = Keyword("on");
        EQUALS = Keyword("equals");
        INTO = Keyword("into");
        ORDERBY = Keyword("orderby");
        ASCENDING = Keyword("ascending");
        DESCENDING = Keyword("descending");
        GROUP = Keyword("group");
        BY = Keyword("by");

        numericLiteral = SkipWhiteSpace(new NumericLiteralToken());
        stringLiteral = SkipWhiteSpace(new StringLiteralToken());
        charLiteral = SkipWhiteSpace(new CharacterLiteralToken());
        interpolatedString = SkipWhiteSpace(new InterpolatedStringToken(expression));

        boolLiteral = TRUE.Then<Expression>(_ => new LiteralExpression(true, LiteralKind.Boolean, "true"))
            .Or(FALSE.Then<Expression>(_ => new LiteralExpression(false, LiteralKind.Boolean, "false")));

        nullLiteral = NULL.Then<Expression>(_ => new LiteralExpression(null, LiteralKind.Null, "null"));

        literal = numericLiteral.Or(stringLiteral).Or(interpolatedString).Or(charLiteral).Or(boolLiteral).Or(nullLiteral);
    }
}
