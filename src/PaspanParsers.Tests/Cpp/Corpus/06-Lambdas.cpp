// Lambda expressions: captures, parameters, specifiers, templates, attributes, immediate calls; fold
// expressions and requires-expressions. Lambdas that capture this are in 08-Classes.cpp.

#include <concepts>

int apply(int (*function)(int), int value) { return function(value); }

void lambdas()
{
    int a = 1, b = 2;
    auto empty = [] {};
    auto no_capture = [](int x) { return x * 2; };
    auto by_value = [a] { return a; };
    auto by_reference = [&b] { b++; };
    auto default_value = [=] { return a + b; };
    auto default_reference = [&] { a = b; };
    auto mixed = [=, &b] { return a + b; };
    auto mixed_reference = [&, a] { return a + b; };
    auto init_capture = [c = a + b, &d = a] { return c + d; };
    auto braced_capture = [e{ a }, f(b)] { return e + f; };
    auto pack_capture = []<typename... T>(T... values) { return [...copies = values] { return sizeof...(copies); }; };
    auto pack_reference = [](auto &... values) { return [&values...] { return (values + ...); }; };
    auto trailing = [](int x) -> long { return x; };
    auto specifiers = [](int x) constexpr noexcept -> int { return x; };
    auto mutable_lambda = [a]() mutable { return ++a; };
    auto mutable_without_parameters = [a] mutable { return ++a; };
    auto static_lambda = [](int x) static { return x; };
    auto consteval_lambda = [](int x) consteval { return x; };
    auto generic = [](auto x, auto &&y) { return x + y; };
    auto templated = []<typename T>(T x) { return x; };
    auto constrained = []<typename T> requires (sizeof(T) > 1) (T x) { return x; };
    auto template_parameters = []<typename T, int N = 2, template <typename> class TT, typename... Rest>(T x) { return x * N; };
    auto concept_parameter = []<std::integral T>(T x) requires std::same_as<T, int> { return x; };
    auto placeholder = [](std::integral auto x) { return x; };
    auto with_attribute = [] [[nodiscard]] (int x) { return x; };
    auto variadic = [](auto... args) { return (args + ... + 0); };
    auto folds = [](auto... args) { return (... + args) * (args * ...) + (0 + ... + args) + (1 * ... * args); };
    auto comma_fold = [](auto &... args) { (++args, ...); };
    auto call_all = [](auto... functions) { return (functions() + ...); };
    auto nested = [](int x) { return [x](int y) { return x + y; }; };
    int immediate = [](int x) { return x + 1; }(41);
    int converted = apply([](int x) { return x - 1; }, 3);
    int unary_plus = apply(+[](int x) { return x; }, 4);
    auto parenthesized = ([] { return 1; })();
    int (*pointer)(int) = [](int x) { return x; };
    auto call_operator = &decltype(no_capture)::operator();
    int through_member = (no_capture.*call_operator)(5) + no_capture.operator()(6);
    int folded = variadic(1, 2, 3) + folds(1, 2) + call_all([] { return 1; }, [] { return 2; });
    (void)empty; (void)no_capture; (void)by_value; (void)by_reference; (void)default_value;
    (void)default_reference; (void)mixed; (void)mixed_reference; (void)init_capture; (void)braced_capture;
    (void)pack_capture; (void)pack_reference; (void)trailing; (void)specifiers; (void)mutable_lambda;
    (void)mutable_without_parameters; (void)static_lambda; (void)consteval_lambda; (void)generic; (void)templated;
    (void)constrained; (void)template_parameters; (void)concept_parameter; (void)placeholder; (void)with_attribute;
    (void)comma_fold; (void)nested; (void)immediate; (void)converted; (void)unary_plus; (void)parenthesized;
    (void)pointer; (void)through_member; (void)folded;
}

void requirements(int value)
{
    bool simple = requires { value + 1; };
    bool parameters = requires (int x, int *p) { x + *p; p[0]; };
    bool typed = requires { typename std::integral_constant<int, 1>::type; };
    bool compound = requires (int x) { { x + 1 } -> std::same_as<int>; { x } noexcept; { x * 2 } noexcept -> std::integral; };
    bool nested = requires { requires sizeof(int) == 4; requires std::integral<long>; };
    auto generic = [](auto x) { return requires { x.size(); }; };
    bool result = generic(1);
    (void)simple; (void)parameters; (void)typed; (void)compound; (void)nested; (void)result;
}
