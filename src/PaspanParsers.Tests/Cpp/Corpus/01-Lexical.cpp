// Lexical elements: literals, identifiers, alternative tokens, digraphs, line splices and comments.
// Only declarations with fundamental types are used, so that the file checks the lexer on its own.

/* A block comment
   over several lines */

// Integer literals
unsigned long long decimal = 18'446'744'073'709'551'615ull;
int octal = 0'17;
int zero = 0;
long hex = 0xDEAD'BEEF & 0Xff;
int binary = 0b1010'1010 | 0B11;
long suffixes = 1l + 2L + 3u + 4U + 5ul + 6LU + 7ll + 8LL + 9uLL + 10Ull;
auto size = 42uz + 7ZU + 3z;
auto large = 9223372036854775807 + 0x8000'0000'0000'0000;

// Floating literals
double floating = 1.0 + .5 + 1. + 1e10 + 1E-10 + 1.5e+3 + 0.1 + 123456789.987654321;
float single = 1.0f + 2.F + 3.14f;
long double extended = 3.14L + 1e-5l;
double hex_float = 0x1.8p1 + 0X.Cp-2 + 0x1p0 + 0x1P+10 + 0xA.Bp-3;
double tiny = 4.9406564584124654e-324;
double infinite = 1e400;

// Character literals
char letter = 'a';
char newline = '\n', tab = '\t', backslash = '\\', quote = '\'', double_quote = '\"', question = '\?';
char null_character = '\0', hex_escape = '\x41', octal_escape = '\101', bell = '\a', backspace = '\b';
char form_feed = '\f', vertical_tab = '\v', carriage_return = '\r', high = '\xFF', high_octal = '\377';
char delimited_hex = '\x{41}', delimited_octal = '\o{102}';
wchar_t wide = L'w';
wchar_t wide_high = L'\xFFFFFFFF';
char8_t utf8 = u8'x';
char16_t utf16 = u'é';
char32_t utf32 = U'\U0001F600';
char32_t delimited_code_point = U'\u{1F600}';
int multicharacter = 'ab';

// String literals
auto plain = "plain \"quoted\" text";
auto escapes = "\a\b\f\n\r\t\v\\\'\"\?\0\x7f\101\x{42}\o{103}";
auto concatenated = "one" " two"
    " three";
auto wide_string = L"wide é \xFFFF";
auto utf8_string = u8"utf-8 é é";
auto utf16_string = u"utf-16 \U0001F600";
auto utf32_string = U"utf-32 \U0001F600 \u{1F600}";
auto raw = R"(raw \n "string")";
auto raw_delimited = R"delim(contains )" and "( )delim";
auto raw_lines = R"(line one
line two)";
auto wide_raw = LR"(wide raw)";
auto utf8_raw = u8R"x(u8 raw)x";
auto mixed = "a" R"(b)" "c";
auto mixed_encoding = "a" u8"b" "c";
auto wide_concatenation = "narrow " L"wide";
auto unicode = "é ñ 日本 😀";
auto hex_digit_after_escape = "\xA" "B";

// Boolean and pointer literals
bool yes = true, no = false;
bool is_null = nullptr == nullptr;

// Identifiers
int _leading, trailing_, with_digits123, CamelCase, $dollar, _Reserved;
int café = 1;
int été = 2;
int ñandú = 3;
int 日本語 = 4;
int αβγ = café + été + ñandú + 日本語;
int x\u{2C7C} = 5;

// Alternative tokens and digraphs
bool alternative(bool a, bool b, int c)
<%
    c and_eq 1;
    c or_eq 2;
    c xor_eq 3;
    return (a and b) or (not a) or (a xor b) or (compl 0 bitand 1 bitor 2) != 0 and c not_eq 3;
%>

// Line splices inside tokens
int spliced_\
identifier = 1 +\
2;
long spliced_number = 12\
34;
int spliced_operator = spliced_identifier <\
= 3 &\
& spliced_number <\
< 1;
auto spliced_string = "one \
two";
bool spliced_keyword = tr\
ue;
int spliced_function(int value)
{
    retu\
rn value;
}

// A line comment \
continued on the next line
int after_comment = 3; /* a block comment */ int after_block = 4;
