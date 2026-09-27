// Expressions: operators of every precedence level, casts, sizeof/alignof, new/delete, throw.

#include <compare>
#include <new>
#include <typeinfo>

struct Base { virtual ~Base() {} int field; int method() { return field; } };
struct Derived : Base { int extra; };
struct Pair { int first, second; auto operator<=>(const Pair &) const = default; };

int values[4] = { 1, 2, 3, 4 };
int global = 0;

int operators(int a, int b, int c, int *p, Base &base, Base *pointer, Derived derived)
{
    int r = a + b * c - a / b % c;
    r = (a + b) * c;
    r = a << 2 >> 1;
    r = a < b && b <= c || a > c && b >= a;
    r = a == b != (c == a);
    r = a & b ^ c | a;
    r = a ? b : c ? a : b;
    r = !a + ~b + -c + +a;
    r = *p + p[1] + 1[p] + *(p + 2);
    r = base.field + pointer->field + base.method() + pointer->method();
    r = a++ + ++b - c-- - --a;
    r += 1; r -= 2; r *= 3; r /= 4; r %= 5;
    r <<= 1; r >>= 1; r &= 7; r |= 8; r ^= 9;
    r = (a, b, c);
    r = a = b = c;
    int *address = &values[2];
    int Base::*member = &Base::field;
    int (Base::*method)() = &Base::method;
    r = base.*member + (pointer->*method)() + (base.*method)();
    bool less = (Pair{1, 2} <=> Pair{1, 3}) < 0;
    return r + *address + less;
}

void casts(double d, const int *cp, Base *base, void *raw)
{
    int a = (int)d;
    int b = int(d);
    int c = static_cast<int>(d);
    int *d1 = const_cast<int *>(cp);
    Derived *e = dynamic_cast<Derived *>(base);
    long f = reinterpret_cast<long>(raw);
    auto g = (unsigned long long)(a + b);
    auto h = (const int *)raw;
    auto i = int{c};
    auto j = auto(c);
    auto k = auto{c};
    (void)d1; (void)e; (void)f; (void)g; (void)h; (void)i; (void)j; (void)k;
}

void sizes()
{
    auto a = sizeof(int);
    auto b = sizeof global;
    auto c = sizeof(global);
    auto d = sizeof values / sizeof values[0];
    auto e = alignof(double);
    auto f = sizeof(Derived *) + sizeof(int[3]);
    bool g = noexcept(sizes());
    const auto &h = typeid(int);
    const auto &i = typeid(global);
    (void)a; (void)b; (void)c; (void)d; (void)e; (void)f; (void)g; (void)h; (void)i;
}

void dynamic_memory()
{
    int *a = new int;
    int *b = new int(5);
    int *c = new int{6};
    int *d = new int[10];
    int *e = new int[3]{ 1, 2, 3 };
    Derived *f = new Derived();
    Base *g = ::new Derived{};
    int (*h)[3] = new int[2][3];
    auto i = new (int *)(nullptr);
    void *buffer = operator new(16);
    int *j = new (buffer) int(7);
    delete a;
    delete[] d;
    ::delete b;
    delete c;
    delete[] e;
    delete f;
    delete g;
    delete[] h;
    delete i;
    using Integer = int;
    j->~Integer();
    operator delete(buffer);
}

int throwing(int x)
{
    if (x < 0)
        throw x;
    return x > 100 ? throw 1, 0 : x;
}

int (*function_pointer)(int) = &throwing;
auto called = (*function_pointer)(1);
