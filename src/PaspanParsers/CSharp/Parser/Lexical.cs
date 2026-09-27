using Paspan;
using Paspan.Fluent;
using static Paspan.Fluent.Parsers;

namespace PaspanParsers.CSharp;

// Lexical level: punctuation, keywords and literals.
public partial class CSharpParser
{
    // Punctuation
    private static Parser<Unit> COMMA, DOT, SEMICOLON, COLON, LPAREN, RPAREN, LBRACE, RBRACE,
        LBRACKET, RBRACKET, LT, GT, EQ, QUESTION, AT;
    private static Parser<string> ARROW;

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

    // Keywords set - includes LINQ contextual keywords to prevent them being parsed as identifiers
    private static readonly HashSet<string> keywords =
    [
        "namespace", "using", "class", "struct", "interface", "enum", "record", "delegate",
        "public", "private", "protected", "internal", "static", "readonly", "const",
        "virtual", "override", "abstract", "sealed", "partial", "async", "extern", "unsafe",
        "volatile", "new", "required", "ref", "out", "in", "params",
        "if", "else", "while", "do", "for", "foreach", "switch", "case", "default",
        "break", "continue", "return", "throw", "try", "catch", "finally", "lock",
        "yield", "await", "var", "void", "get", "set", "init", "add", "remove",
        "where", "this", "base", "operator", "implicit", "explicit", "as", "is",
        "typeof", "sizeof", "nameof", "when", "true", "false", "null",
        "object", "string", "bool", "byte", "sbyte", "short", "ushort",
        "int", "uint", "long", "ulong", "float", "double", "decimal", "char", "dynamic",
        // LINQ contextual keywords - treated as keywords to ensure proper parsing
        "from", "select", "let", "join", "on", "equals", "into",
        "orderby", "ascending", "descending", "group", "by"
    ];

    // Literals
    private static Parser<Expression> integerLiteral, decimalLiteral, stringLiteral, charLiteral,
        boolLiteral, nullLiteral, literal;

    private static Parser<string> Keyword(string text) => Terms.Keyword(text, caseInsensitive: false);

    private static void InitializeLexical()
    {
        COMMA = Terms.Char(',');
        DOT = Terms.Char('.');
        SEMICOLON = Terms.Char(';');
        COLON = Terms.Char(':');
        LPAREN = Terms.Char('(');
        RPAREN = Terms.Char(')');
        LBRACE = Terms.Char('{');
        RBRACE = Terms.Char('}');
        LBRACKET = Terms.Char('[');
        RBRACKET = Terms.Char(']');
        LT = Terms.Char('<');
        GT = Terms.Char('>');
        EQ = Terms.Char('=');
        QUESTION = Terms.Char('?');
        AT = Terms.Char('@');
        ARROW = Terms.Text("=>");

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

        integerLiteral = Terms.Integer()
            .Then<Expression>(i => new LiteralExpression((int)i, LiteralKind.Integer));

        decimalLiteral = Terms.Decimal()
            .Then<Expression>(d => new LiteralExpression((decimal)d, LiteralKind.Real));

        stringLiteral = Terms.String(StringLiteralQuotes.Double)
            .Then<Expression>(s => new LiteralExpression(s.ToString(), LiteralKind.String));

        charLiteral = Between(Terms.Char('\''), Literals.NoneOf("'"), Terms.Char('\''))
            .Then<Expression>(c => new LiteralExpression(c.ToString()[0], LiteralKind.Character));

        boolLiteral = TRUE.Then<Expression>(new LiteralExpression(true, LiteralKind.Boolean))
            .Or(FALSE.Then<Expression>(new LiteralExpression(false, LiteralKind.Boolean)));

        nullLiteral = NULL.Then<Expression>(new LiteralExpression(null, LiteralKind.Null));

        literal = decimalLiteral.Or(integerLiteral).Or(stringLiteral).Or(charLiteral).Or(boolLiteral).Or(nullLiteral);
    }
}
