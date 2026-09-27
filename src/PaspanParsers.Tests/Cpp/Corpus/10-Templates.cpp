// Templates: class, function, variable and alias templates, specializations, dependent names.

template <typename T>
struct Box
{
    T value;
    Box(T v) : value(v) {}
    T get() const { return value; }
    template <typename U> U as() const { return static_cast<U>(value); }
    template <typename U> struct Rebind { using type = Box<U>; };
    using value_type = T;
};

template <typename T>
T maximum(T a, T b) { return a > b ? a : b; }

template <class T, int N = 4>
struct Array
{
    T data[N];
    static constexpr int size = N;
};

template <typename T> constexpr T zero = T(0);
template <typename T> using Pointer = T *;
template <typename T> using BoxOf = Box<T>;

template <>
struct Box<void>
{
    void get() const {}
};

template <typename T>
struct Box<T *>
{
    T *value;
};

template <>
int maximum<int>(int a, int b) { return a > b ? a : b; }

template <typename K, typename V>
struct Map {};

template <typename V>
struct Map<int, V> { V only; };

template <template <typename> class Container, typename T>
struct Wrapper
{
    Container<T> inner;
};

template <auto Value> struct Constant { static constexpr auto value = Value; };
template <int... Values> struct Sequence {};
template <typename T, T Default = T{}> struct WithDefault {};
template <bool B, typename T = void> struct EnableIf {};
template <typename T> struct EnableIf<true, T> { using type = T; };

template <typename T>
typename Box<T>::value_type unwrap(const Box<T> &box)
{
    typename Box<T>::value_type result = box.get();
    return result + box.template as<T>();
}

template <typename T>
struct Derived : Box<T>
{
    using Base = Box<T>;
    using Base::Base;
    using typename Base::value_type;
    template <typename U> using Other = typename Base::template Rebind<U>::type;

    value_type twice() const { return this->value * 2 + Base::get(); }
};

template struct Box<long>;
extern template struct Box<short>;
template int maximum<int>(int, int);

template <typename T> Box(T) -> Box<T>;
Box deduced(42);

Array<int> a;
Array<double, 8> b;
Array<Array<int, 2>, 2> nested;
Box<Box<int>> boxed{ Box<int>(1) };
Wrapper<Box, int> wrapped{ Box<int>(2) };
Map<int, char> map;
Constant<'x'> letter;
Sequence<1, 2, 3> numbers;
Pointer<int> p = nullptr;
BoxOf<float> float_box(1.0f);
int biggest = maximum(1, 2) + maximum<long>(3, 4) + zero<int>;
bool comparison = Constant<(1 > 0)>::value;
int shifted = Constant<(8 >> 1)>::value;
Box<int (*)(int)> function_box(nullptr);
EnableIf<true, int>::type enabled = 1;

template <typename T>
void dependent(T t)
{
    typename T::type *pointer = nullptr;
    auto size = T::template size<int>();
    t.template call<0>();
    (void)pointer; (void)size;
}
