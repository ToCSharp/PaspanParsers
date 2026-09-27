// Enumerations: unscoped and scoped, underlying types, opaque declarations, using enum.

enum Unscoped { A, B = 5, C, };
enum class Scoped { First, Second = 10, Third };
enum struct AlsoScoped : char { X = 'x', Y = 'y' };
enum Sized : unsigned long long { Big = 1ull << 40 };
enum class Opaque : int;
enum Forward : short;
enum class Opaque : int { One = 1 };
enum { Anonymous1, Anonymous2 };
enum Empty {};
enum class EmptyScoped {};
enum WithAttributes { Old [[deprecated]], New [[maybe_unused]] = 2 };

typedef enum { Red, Green, Blue } Color;

struct Holder
{
    enum Kind { Small, Large } kind = Small;
    enum class Mode : unsigned char { On, Off };
    Mode mode = Mode::On;
};

int use(Scoped scoped)
{
    using enum Scoped;
    switch (scoped)
    {
    case First: return 1;
    case Second: return 2;
    case Third: return 3;
    }

    Color color = Green;
    Holder::Kind kind = Holder::Large;
    Holder::Mode mode = Holder::Mode::Off;
    enum Unscoped local = C;
    return static_cast<int>(AlsoScoped::X) + color + kind + static_cast<int>(mode) + local + Unscoped::A;
}
