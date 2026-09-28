// Declarations and templates beyond the thematic files: members of class templates defined outside them,
// nested templates, member classes defined outside, friends, packs of abstract parameters, local classes,
// using forms in blocks, throw(), anonymous members and names that scopes make visible.

template <typename T>
struct Stack
{
    struct Node;
    static int instances;
    T top() const;
    template <typename U> void push(U value);
    template <typename U> operator U() const { return U(); }
    template <typename U> friend struct Visitor;
    friend Stack operator+(const Stack &a, const Stack &) { return a; }
    explicit(false) Stack(int) {}
    Stack() = default;
    ~Stack() noexcept(true) {}
};

template <typename T> struct Stack<T>::Node { T value; Node *next; };
template <typename T> int Stack<T>::instances = 0;
template <typename T> T Stack<T>::top() const { return T(); }
template <typename T> template <typename U> void Stack<T>::push(U value) { (void)value; }
template <> int Stack<int>::top() const { return 0; }
template <> template <> void Stack<char>::push<int>(int) {}

template <typename T> constexpr int rank = 0;
template <typename T> constexpr int rank<T *> = rank<T> + 1;
template <> constexpr int rank<void> = -1;

template <typename... Ts> void take(Ts...);
template <typename... Ts> void forward_refs(Ts &&...);
template <typename... Ts> void variadic_c(Ts..., ...);
template <typename T, typename... Ts> void mixed(const T &, Ts *...);

template <class T>
    requires (sizeof(T) > 1)
struct Wide { T value; };

template <class T> struct Wrapper { T inner; };
template <class T> Wrapper(T) -> Wrapper<T>;
Wrapper wrapped{ 1.5 };

namespace geometry
{
    struct Point { int x, y; };
    enum class Axis : unsigned char { X, Y };
    int length(Point p);

    namespace detail
    {
        inline int square(int v) { return v * v; }
    }
}

int geometry::length(Point p) { return detail::square(p.x) + detail::square(p.y); }

struct Outer
{
    struct Inner;
    enum Mode : int;
    Inner *make();
};

struct Outer::Inner { int depth = 1; };
enum Outer::Mode : int { Quiet, Loud };
Outer::Inner *Outer::make() { return new Inner{}; }

typedef struct { int left, right; } Interval;
typedef enum { Off, On } Switch;

struct Anonymous
{
    union
    {
        int as_int;
        float as_float;
    };

    struct { unsigned low : 4, high : 4; } nibbles;
    int : 0;
    unsigned flag : 1 = 1;
};

class Final final {};
struct Base { virtual ~Base() = default; virtual int value() const = 0; };
struct Derived final : Base { int value() const final override { return 1; } };

void no_exceptions() throw ();

void blocks()
{
    using geometry::Point;
    using namespace geometry::detail;
    using enum geometry::Axis;
    namespace g = geometry;
    using Length = int;
    struct Local { int value; } local{ square(2) };
    class Counter
    {
    public:
        int count = 0;
        void add() { ++count; }
    } counter;
    int values[] = { 1, 2 };
    for (using Index = int; Index i : values)
        counter.add();
    Point p{ 1, 2 };
    Length length = g::length(p) + local.value + counter.count + static_cast<int>(X);
    (void)length;
}

extern "C" typedef int (*Callback)(int);
extern "C" int callback_target(int value);
Callback callback = callback_target;
