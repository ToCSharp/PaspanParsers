// Variadic templates: parameter packs, pack expansions, sizeof..., fold expressions.

template <typename... Types>
struct TypeList
{
    static constexpr auto size = sizeof...(Types);
};

template <typename First, typename... Rest>
struct Head
{
    using type = First;
    using tail = TypeList<Rest...>;
};

template <typename... Args>
auto sum(Args... args) { return (args + ... + 0); }

template <typename... Args>
auto product(Args... args) { return (1 * ... * args); }

template <typename... Args>
bool all(Args... args) { return (... && args); }

template <typename... Args>
bool any(Args... args) { return (args || ...); }

template <typename... Args>
int count(Args &&...args) { return static_cast<int>(sizeof...(args)); }

void sink(int, int, int);

template <typename... Args>
void forward_all(Args &&...args)
{
    sink(static_cast<Args &&>(args)...);
}

template <typename... Bases>
struct Inherit : Bases...
{
    using Bases::operator()...;
};

template <typename T, typename... Rest>
constexpr int first_size = sizeof(T);

template <int... N>
constexpr int total = (N + ...);

template <typename... Ts>
void expand_in_braces(Ts... values)
{
    int array[] = { (static_cast<void>(values), 0)... };
    (void)array;
}

template <typename... Ts>
auto capture(Ts... values)
{
    return [values...] { return sum(values...); };
}

template <template <typename...> class List, typename... Ts>
List<Ts..., int> append();

struct A { void operator()(int) {} };
struct B { void operator()(double) {} };

int use()
{
    Inherit<A, B> both;
    both(1);
    both(2.0);
    return sum(1, 2, 3) + product(2, 3) + all(true, true) + any(false) + count(1, 'a')
        + TypeList<int, char, long>::size + total<1, 2, 3> + first_size<int, double> + capture(1, 2)();
}
