// C++23 features: explicit object parameters, if consteval, multidimensional subscripts,
// static operators, auto(x), size_t literals, labels at the end of blocks.

struct Counter
{
    int count = 0;

    void increment(this Counter &self) { ++self.count; }
    int get(this const Counter &self) { return self.count; }
    template <typename Self> auto &&value(this Self &&self) { return static_cast<Self &&>(self).count; }
    static int operator()(int x) { return x; }
    int operator[](int i, int j) const { return i * j; }
    int operator[]() const { return 0; }
};

constexpr int compile_time(int x)
{
    if consteval
    {
        return x * 2;
    }
    else
    {
        return x;
    }
}

int use(Counter counter)
{
    counter.increment();
    auto copy = auto(counter);
    auto size = sizeof(int) + 1uz;
    auto recursive = [](this auto self, int n) -> int { return n <= 1 ? 1 : n * self(n - 1); };
    int result = counter.get() + copy.value() + Counter::operator()(1) + counter[2, 3] + counter[]
        + compile_time(4) + static_cast<int>(size) + recursive(5);
    {
        goto end;
    end:
    }
    return result;
}
