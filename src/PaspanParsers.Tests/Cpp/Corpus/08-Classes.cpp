// Classes: members, access, inheritance, nested types, friends, bit-fields, static members; expressions
// with classes (member access, pointers to members, this in lambdas).

#include <compare>

class Forward;

struct Empty {};

struct Aggregate
{
    int a;
    double b = 1.5;
    int c{ 2 };
    static const int constant = 3;
    static inline int shared = 4;
    static constexpr double pi = 3.14;
    mutable int cache = 0;
    int array[3];
    int *pointer, value, &reference = value;
};

class Access
{
public:
    int visible;

protected:
    int inherited;

private:
    int hidden;
    friend class Forward;
    friend struct Aggregate;
    friend int peek(const Access &access) { return access.hidden; }
    friend void other(Access &);

public:
    int get() const { return hidden; }
    void set(int value) { hidden = value; }
    int &ref() & { return hidden; }
    int ref() && { return hidden; }
    int volatile_method() volatile;
    static int count();
    virtual void render() const = 0;
    virtual ~Access() = default;
};

int Access::count() { return 0; }

struct Flags
{
    unsigned read : 1;
    unsigned write : 1 = 1;
    unsigned : 2;
    unsigned execute : 1 {};
    int : 0;
};

class Base
{
public:
    virtual int area() const { return 0; }
    virtual void draw() = 0;
    virtual ~Base() {}
};

class Middle : public Base
{
public:
    int area() const override { return 1; }
    void draw() override final {}
};

class Leaf final : public Middle, protected virtual Empty, private Flags
{
};

struct Defaulted : Base, virtual Aggregate
{
    void draw() override {}
};

class Outer
{
public:
    struct Inner
    {
        int value;
        struct Deeper { int depth; };
    };

    enum Kind { First, Second };
    using Alias = Inner;
    typedef Inner::Deeper Deep;

    Inner make() const;
    Alias alias;
    Deep deep;
    Kind kind = First;
};

Outer::Inner Outer::make() const { return Inner{ 1 }; }
Outer::Inner::Deeper deeper{ 2 };

union Variant
{
    int integer;
    float real;
    struct { short low, high; } halves;
};

struct WithAnonymous
{
    union { int as_int; float as_float; };
    struct { int x, y; };
};

struct Local
{
    void method()
    {
        struct Inside { int x; } inside{ 1 };
        class { public: int y; } unnamed;
        unnamed.y = inside.x;
    }
};

struct Point { int x, y; } origin{ 0, 0 }, *pointer = &origin;

// Expressions with classes

struct Shape { virtual ~Shape() {} int field; int method() { return field; } };
struct Square : Shape { int extra; };
struct Pair { int first, second; auto operator<=>(const Pair &) const = default; };

int member_access(Shape &shape, Shape *pointer, Square square)
{
    int r = shape.field + pointer->field + shape.method() + pointer->method() + square.extra + square.Shape::field;
    int Shape::*member = &Shape::field;
    int (Shape::*method)() = &Shape::method;
    r = shape.*member + (pointer->*method)() + (shape.*method)();
    bool less = (Pair{1, 2} <=> Pair{1, 3}) < 0;
    Square *derived = dynamic_cast<Square *>(pointer);
    Shape *created = new Square();
    Shape *global = ::new Square{};
    delete created;
    delete global;
    return r + less + (derived != nullptr);
}

struct Widget
{
    int size = 0;

    auto getter()
    {
        return [this] { return size; };
    }

    auto copier()
    {
        return [*this]() mutable { return ++size; };
    }

    auto everything()
    {
        return [=, this](int extra) { return this->size + extra; };
    }
};
