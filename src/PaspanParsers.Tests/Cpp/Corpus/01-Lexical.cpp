// Lexical elements: literals, alternative tokens, digraphs, line splices, comments.

/* A block comment
   over several lines */
unsigned long long decimal = 18'446'744'073'709'551'615ull;
int octal = 0'17;
int hex = 0xDEAD'BEEF & 0Xff;
int binary = 0b1010'1010 | 0B11;
long suffixes = 1l + 2L + 3u + 4U + 5ul + 6LU + 7ll + 8LL;
decltype(sizeof 0) size = 42uz + 7ZU;
double floating = 1.0 + .5 + 1. + 1e10 + 1E-10 + 1.5e+3;
float single = 1.0f + 2.F;
long double extended = 3.14L;
double hex_float = 0x1.8p1 + 0X.Cp-2 + 0x1p0;

char c = 'a';
char escapes[] = { '\n', '\t', '\\', '\'', '\"', '\?', '\0', '\x41', '\101', '\a', '\b', '\f', '\v', '\r' };
wchar_t wide = L'w';
char8_t utf8 = u8'x';
char16_t utf16 = u'é';
char32_t utf32 = U'\U0001F600';
int multichar = 'ab';

const char *plain = "plain \"quoted\" text";
const char *concatenated = "one" " two"
    " three";
const wchar_t *wide_string = L"wide";
const char8_t *utf8_string = u8"utf-8 é";
const char16_t *utf16_string = u"utf-16";
const char32_t *utf32_string = U"utf-32";
const char *raw = R"(raw \n "string")";
const char *raw_delimited = R"delim(contains )" and "( )delim";
const char *raw_lines = R"(line one
line two)";
const wchar_t *wide_raw = LR"(wide raw)";
const char *mixed = "a" R"(b)" "c";

// Identifiers
int _leading, trailing_, with_digits123, CamelCase, $dollar;
int café = 1;
int ñandú = 2;

// Alternative tokens and digraphs
bool alternative(bool a, bool b)
<%
    int array<:2:> = <%1, 2%>;
    return (a and b) or (not a) or (a xor b) or (compl 0 bitand 1 bitor 2) != 0 and array<:0:> not_eq 3;
%>

void compound_alternatives(int &x)
{
    x and_eq 1;
    x or_eq 2;
    x xor_eq 3;
}

// Line splices
int spliced_\
identifier = 1 +\
2;

// A line comment \
continued on the next line
int after_comment = 3;
