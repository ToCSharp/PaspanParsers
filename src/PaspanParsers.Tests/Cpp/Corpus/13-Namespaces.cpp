// Namespaces, using declarations and directives, linkage specifications, static_assert.

namespace outer
{
    int value = 1;

    namespace inner
    {
        int value = 2;
    }

    inline namespace v1
    {
        int versioned = 3;
    }

    namespace
    {
        int internal = 4;
    }
}

namespace outer::inner
{
    int more = 5;
}

namespace outer::inline v2
{
    int newest = 6;
}

namespace alias = outer::inner;
namespace
{
    int file_local = 7;
}

namespace outer
{
    void extended() {}
}

using outer::value;
using outer::extended, outer::inner::more;
using namespace outer::inner;

extern "C" int c_function(int);
extern "C"
{
    int c_first(void);
    int c_second(int);
}
extern "C++" int cpp_function();
extern "C" { }

static_assert(sizeof(int) >= 2);
static_assert(true, "message");

struct Base { void f(); int shared; };
struct Derived : Base
{
    using Base::f;
    using Base::shared;
};

int use()
{
    using namespace outer;
    using alias::more;
    namespace local = outer::inner;
    return ::outer::value + alias::value + local::more + versioned + outer::v1::versioned + v2::newest + file_local;
}

int ::Base::*global_member = &::Base::shared;
