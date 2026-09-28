// Special member functions, operators and conversions.

struct Resource
{
    int *data;
    int size;

    Resource() : data(nullptr), size(0) {}
    explicit Resource(int n) : data(new int[n]), size{ n } {}
    Resource(int n, int fill) : Resource(n) { for (int i = 0; i < n; ++i) data[i] = fill; }
    Resource(const Resource &other) : Resource(other.size) {}
    Resource(Resource &&other) noexcept : data(other.data), size(other.size) { other.data = nullptr; }
    ~Resource() { delete[] data; }

    Resource &operator=(const Resource &other) { size = other.size; return *this; }
    Resource &operator=(Resource &&other) noexcept = default;

    int &operator[](int index) { return data[index]; }
    int operator[](int row, int column) const { return data[row * size + column]; }
    int operator()(int a, int b) const { return a + b; }
    static int operator()(int a) { return a; }
    Resource &operator++() { ++size; return *this; }
    Resource operator++(int) { Resource copy(size); ++size; return copy; }
    bool operator!() const { return size == 0; }
    int *operator->() { return data; }
    int operator*() const { return *data; }
    Resource *operator&() { return this; }
    void operator,(int) {}

    explicit operator bool() const { return size != 0; }
    operator int() const { return size; }
    explicit(sizeof(int) == 4) operator long() const { return size; }
    operator const int *() const { return data; }

    friend bool operator==(const Resource &a, const Resource &b) { return a.size == b.size; }
    friend Resource operator+(const Resource &a, const Resource &b);

    void *operator new(decltype(sizeof 0) size);
    void operator delete(void *pointer);
    void *operator new[](decltype(sizeof 0) size);
    void operator delete[](void *pointer) noexcept;
};

Resource operator+(const Resource &a, const Resource &b) { return Resource(a.size + b.size); }
bool operator<(const Resource &a, const Resource &b) { return a.size < b.size; }
Resource &operator<<(Resource &r, int v) { r.size += v; return r; }
Resource &operator+=(Resource &r, int v) { r.size += v; return r; }
bool operator&&(const Resource &a, int b) { return a.size && b; }
Resource &operator-(Resource &r) { return r; }

long double operator""_km(long double value) { return value * 1000; }
unsigned long long operator""_count(unsigned long long value) { return value; }
const char *operator""_raw(const char *text) { return text; }
int operator""_len(const char *text, decltype(sizeof 0) length) { return static_cast<int>(length); }

auto distance = 1.5_km;
auto items = 42_count;
auto raw = 123_raw;
auto length = "hello"_len;

struct Deleted
{
    Deleted() = delete;
    Deleted(const Deleted &) = delete;
    Deleted &operator=(const Deleted &) = delete;
    void only_int(int);
    void only_int(double) = delete;
};

struct Inheriting : Resource
{
    using Resource::Resource;
    using Resource::operator=;
};

struct Member
{
    int a, b;
    Member(int x) try : a(x), b(x * 2)
    {
    }
    catch (...)
    {
    }
};

void use()
{
    Resource r(3, 7);
    Resource s = r;
    s = static_cast<Resource &&>(r);
    int x = s[0] + s[1, 2] + s(1, 2) + Resource::operator()(3) + *s;
    s++;
    ++s;
    s << 1;
    s += 2;
    bool b = !s || s && 1;
    int converted = s;
    long wide = static_cast<long>(s);
    s.operator++();
    s.~Resource();
    (void)x; (void)b; (void)converted; (void)wide;
}
