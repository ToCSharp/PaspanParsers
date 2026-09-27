// Concepts and constraints: concept definitions, requires clauses and expressions, constrained auto.

template <typename T, typename U>
constexpr bool is_same = false;

template <typename T>
constexpr bool is_same<T, T> = true;

template <typename T>
concept Integral = is_same<T, int> || is_same<T, long> || is_same<T, short>;

template <typename T>
concept Addable = requires(T a, T b) { a + b; };

template <typename T>
concept Container = requires(T c) {
    typename T::value_type;
    { c.size() } -> Integral;
    { c.begin() } noexcept;
    c.size();
    requires sizeof(T) > 0;
};

template <typename T, typename U>
concept SameAs = is_same<T, U> && is_same<U, T>;

template <typename T>
concept Printable = Addable<T> && requires { sizeof(T); };

template <Integral T>
T twice(T value) { return value * 2; }

template <typename T>
    requires Addable<T>
T add(T a, T b) { return a + b; }

template <typename T>
T subtract(T a, T b) requires Integral<T> { return a - b; }

template <typename T>
    requires (sizeof(T) >= 4) && Integral<T>
T large(T value) { return value; }

template <typename T>
    requires requires(T t) { t + 1; }
T increment(T value) { return value + 1; }

auto square(Integral auto value) { return value * value; }
Integral auto constrained_result() { return 42; }
void constrained_parameters(Addable auto a, SameAs<int> auto b) { (void)a; (void)b; }

template <typename T>
struct Holder
{
    T value;
    void only_integral() requires Integral<T> {}
    template <typename U> requires SameAs<T, U> void same(U) {}
};

template <typename T>
    requires Integral<T>
struct Holder<T *> {};

struct Vector
{
    using value_type = int;
    int size() const { return 0; }
    int *begin() const noexcept { return nullptr; }
};

static_assert(Container<Vector>);
static_assert(!Container<int>, "int is not a container");
static_assert(Integral<decltype(square(3))>);

int use()
{
    Integral auto a = twice(3);
    const Addable auto &b = add(1, 2);
    return a + b + subtract(5, 3) + large(7) + increment(8) + constrained_result();
}
