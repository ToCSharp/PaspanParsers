// C++20 modules: the global module fragment, a module interface and its exports, and the private module
// fragment. Imports need compiled modules, so they are checked by unit tests.

module;

#include <cstddef>

export module geometry.shapes;

export int area(int width, int height) { return width * height; }

export
{
    struct Rectangle { int width, height; };
    int perimeter(const Rectangle &r);
    constexpr int sides = 4;
}

export namespace geometry
{
    template <typename T>
    T twice(T value) { return value * 2; }
}

export using Size = std::size_t;

namespace internal
{
    int helper() { return 1; }
}

module :private;

int perimeter(const Rectangle &r) { return 2 * (r.width + r.height) + internal::helper() - 1; }
