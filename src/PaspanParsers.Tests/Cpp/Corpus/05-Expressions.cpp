// Expressions: operators of every precedence level, casts, sizeof/alignof, new/delete, throw, braced lists,
// member access through classes of the standard library. Expressions with classes of the file are in 08-Classes.cpp.

#include <compare>
#include <new>
#include <typeinfo>
#include <initializer_list>

int values[4] = { 1, 2, 3, 4 };
int global = 0;
typedef int Integer;

int operators(int a, int b, int c, int *p)
{
    int r = a + b * c - a / b % c;
    r = (a + b) * c;
    r = a << 2 >> 1;
    r = a < b && b <= c || a > c && b >= a;
    r = a == b != (c == a);
    r = a & b ^ c | a;
    r = a ? b : c ? a : b;
    r = a ?: b;
    r = !a + ~b + -c + +a;
    r = - -a + - - -b + !!c;
    r = *p + p[1] + 1[p] + *(p + 2);
    r = a++ + ++b - c-- - --a;
    r += 1; r -= 2; r *= 3; r /= 4; r %= 5;
    r <<= 1; r >>= 1; r &= 7; r |= 8; r ^= 9;
    r = (a, b, c);
    r = a = b = c;
    r = {};
    r = { 5 };
    r = a and b or not c;
    r = (a bitand b) bitor (compl c xor a);
    (a ? r : b) = 1;
    a > b ? r = 1 : r = 2;
    int *address = &values[2];
    bool ordered = (a <=> b) < 0 && (1.0 <=> 2.0) != 0 && (a <=> b) == std::strong_ordering::less;
    return r + *address + ordered;
}

void members(const std::type_info &info, const std::type_info *pointer, std::initializer_list<int> list)
{
    bool same = info == typeid(int) && *pointer != info;
    const char *name = info.name();
    auto hash = pointer->hash_code() + typeid(long).hash_code();
    bool before = info.before(*pointer) || pointer->before(info) || (*pointer).before(info);
    auto size = list.size() + list.end() - list.begin();
    bool (std::type_info::*method)(const std::type_info &) const noexcept = &std::type_info::before;
    bool called = (info.*method)(typeid(int)) && (pointer->*method)(typeid(char));
    const int *first = list.begin();
    (void)same; (void)name; (void)hash; (void)before; (void)size; (void)called; (void)first;
}

void destroy(int *p)
{
    p->~Integer();
    p->Integer::~Integer();
    (*p).~Integer();
}

void casts(double d, const int *cp, void *raw, const std::type_info &info)
{
    int a = (int)d;
    int b = int(d);
    int c = static_cast<int>(d);
    int *d1 = const_cast<int *>(cp);
    long f = reinterpret_cast<long>(raw);
    auto g = (unsigned long long)(a + b);
    auto h = (const int *)raw;
    auto i = int{c};
    auto j = auto(c);
    auto k = auto{c};
    auto l = (Integer)-a;
    auto m = (Integer)(a) + (a) - b;
    auto n = unsigned(a) + char{'x'} + int() + Integer(1) + Integer{2};
    auto o = decltype(a)(b) + decltype(d){};
    const std::type_info &e = dynamic_cast<const std::type_info &>(info);
    auto s = static_cast<const std::nothrow_t *>(raw);
    auto t = (int (*)(int))raw;
    (void)d1; (void)e; (void)f; (void)g; (void)h; (void)i; (void)j; (void)k; (void)l; (void)m; (void)n; (void)o; (void)s; (void)t;
}

void sizes()
{
    auto a = sizeof(int);
    auto b = sizeof global;
    auto c = sizeof(global);
    auto d = sizeof values / sizeof values[0];
    auto e = alignof(double);
    auto f = sizeof(int *) + sizeof(int[3]) + sizeof(int (*)(int)) + sizeof(std::size_t);
    bool g = noexcept(sizes());
    const std::type_info &h = typeid(int);
    const std::type_info &i = typeid(global);
    auto j = sizeof(values) * 2 + sizeof (int) * 2 + sizeof -global;
    auto k = __alignof__(double) + __alignof(global) + _Alignof(int);
    (void)a; (void)b; (void)c; (void)d; (void)e; (void)f; (void)g; (void)h; (void)i; (void)j; (void)k;
}

void dynamic_memory()
{
    int *a = new int;
    int *b = new int(5);
    int *c = new int{6};
    int *d = new int[10];
    int *e = new int[3]{ 1, 2, 3 };
    int *f = new int[]{ 4, 5 };
    int (*h)[3] = new int[2][3];
    int **i = new (int *)(nullptr);
    int **ii = new int *[2];
    void *buffer = operator new(16);
    int *j = new (buffer) int(7);
    int *k = ::new (std::nothrow) int;
    auto l = new auto(1);
    const int *m = new const int(8);
    delete a;
    delete[] d;
    ::delete b;
    delete c;
    delete[] e;
    delete[] f;
    delete[] h;
    delete i;
    delete[] ii;
    delete k;
    delete l;
    delete m;
    ::operator delete(buffer);
    (void)j;
}

int throwing(int x)
{
    if (x < 0)
        throw x;
    if (x == 0)
        throw;
    return x > 100 ? throw 1, 0 : x;
}

int (*function_pointer)(int) = &throwing;
auto called = (*function_pointer)(1) + function_pointer(2) + (&throwing)(3);
const char *text = "abc" + 1;
char letter = "abc"[1];
int direct(1), braced{ 2 }, copy_braced = { 3 }, empty{}, parenthesized((4)), list_array[]{ 5, 6, };
long long operator""_k(unsigned long long value) { return value * 1000; }
long long thousands = 2_k + operator""_k(3);
