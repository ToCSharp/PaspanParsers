// Ambiguities the parser must resolve like clang: declaration or expression, casts, template arguments.

struct U { U() {} };

struct T
{
    T() {}
    T(int) {}
    T(U) {}
    T(bool) {}
    T operator*(int) const { return *this; }
    int operator()(int) const { return 0; }
};

bool operator==(U, U) { return true; }

int a = 1, b = 2, c = 3;
int x = 0;

template <typename V> struct Tmpl { static const int value = 1; };
template <int N> struct Num { static const int value = N; };
template <typename V> int f(V) { return 0; }

void declarations_or_expressions()
{
    T(y);                   // a declaration of y
    T(*p);                  // a declaration of a pointer p
    T(z) = 5;               // a declaration of z
    T t1(1);                // a variable
    T t2();                 // a function declaration (the most vexing parse)
    T t3(U());              // a function declaration taking a function
    T t4((U()));            // a variable: the extra parentheses make it an expression
    T t5{ U{} == U{} };     // not a function
    a * b;                  // a multiplication: a is a variable
    T * q;                  // a declaration: T is a type
    int(c);                 // a declaration of c shadowing the global
    (int)(a);               // a cast, as an expression statement
    T(a) * 2;               // an expression: T(a) is a functional cast
    x = a < b > c;          // comparisons: a is not a template
    (void)y; (void)p; (void)z; (void)q; (void)t1; (void)t4; (void)t5; (void)c;
}

void templates_or_comparisons()
{
    bool less = a < b;
    int value = Tmpl<int>::value;
    int shifted = Num<(8 >> 2)>::value;
    int compared = Num<(1 > 0)>::value;
    int called = f<int>(1);
    Tmpl<Tmpl<int>> nested;
    int greater = a > b >> 1;
    bool chain = a < b && b > c;
    (void)less; (void)value; (void)shifted; (void)compared; (void)called; (void)nested; (void)greater; (void)chain;
}

void casts_or_parentheses(int n)
{
    int r1 = (a) - n;       // a subtraction: a is a variable
    int r2 = (int) - n;     // a cast of -n
    int r3 = (int)(n);      // a cast
    int r4 = T(n)(n);       // a functional cast, then a call of its operator()
    int r5 = sizeof(int);   // a type
    int r6 = sizeof(a);     // an expression
    int r7 = sizeof(int) * 2;
    int r8 = sizeof (a) * 2;
    (void)r1; (void)r2; (void)r3; (void)r4; (void)r5; (void)r6; (void)r7; (void)r8;
}
