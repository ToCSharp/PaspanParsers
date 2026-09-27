// Lambda expressions: captures, parameters, specifiers, templates, attributes, immediate calls.

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
        return [=, this](int extra) { return size + extra; };
    }
};

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
    auto pack_capture = []<typename... T>(T... values) { return [...copies = values] { return sizeof...(copies); }; };
    auto trailing = [](int x) -> long { return x; };
    auto specifiers = [](int x) constexpr noexcept -> int { return x; };
    auto mutable_lambda = [a]() mutable { return ++a; };
    auto static_lambda = [](int x) static { return x; };
    auto consteval_lambda = [](int x) consteval { return x; };
    auto generic = [](auto x, auto &&y) { return x + y; };
    auto templated = []<typename T>(T x) { return x; };
    auto constrained = []<typename T> requires (sizeof(T) > 1) (T x) { return x; };
    auto with_attribute = [] [[nodiscard]] (int x) { return x; };
    auto variadic = [](auto... args) { return (args + ... + 0); };
    auto nested = [](int x) { return [x](int y) { return x + y; }; };
    int immediate = [](int x) { return x + 1; }(41);
    int converted = apply([](int x) { return x - 1; }, 3);
    int unary_plus = apply(+[](int x) { return x; }, 4);
    auto parenthesized = ([] { return 1; })();
    (void)empty; (void)no_capture; (void)by_value; (void)by_reference; (void)default_value;
    (void)default_reference; (void)mixed; (void)mixed_reference; (void)init_capture; (void)pack_capture;
    (void)trailing; (void)specifiers; (void)mutable_lambda; (void)static_lambda; (void)consteval_lambda;
    (void)generic; (void)templated; (void)constrained; (void)with_attribute; (void)variadic; (void)nested;
    (void)immediate; (void)converted; (void)unary_plus; (void)parenthesized;
}
