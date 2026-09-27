// Classes: members, access, inheritance, nested types, friends, bit-fields, static members.

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
