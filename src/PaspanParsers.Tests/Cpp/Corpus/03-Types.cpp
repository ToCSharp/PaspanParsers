// Type specifiers: fundamental types, cv-qualifiers, elaborated and named types, aliases, decltype, auto.

struct Point { int x, y; };
class Shape;
union Value { int i; float f; };
enum Color { Red, Green };

unsigned u1;
signed s1;
short int si;
unsigned short us;
long int li;
long long ll;
unsigned long long ull;
int long unsigned long reordered;
long double ld;
signed char sc;
unsigned char uc;
char8_t c8;
char16_t c16;
char32_t c32;
wchar_t wc;
bool flag;

const int ci = 1;
int const ic = 2;
volatile int vi;
const volatile int cvi = 3;
volatile const unsigned int vcu = 4;

struct Point p1;
Point p2;
const Point cp = {};
class Shape *shape;
union Value value;
enum Color color = Red;
::Point global_point;

typedef int Integer;
typedef unsigned long Size, *SizePointer;
typedef struct Point PointType;
typedef struct { int a; } Anonymous;
using Real = double;
using Callback = void (*)(int);
using Matrix = int[3][3];

Integer typed = 1;
Real real = 2.0;
Callback callback = nullptr;
Matrix identity = {};

decltype(ci) copy = ci;
decltype((copy)) reference = copy;
decltype(auto) deduced = copy;
auto automatic = 1;
const auto &automatic_reference = automatic;
auto *automatic_pointer = &automatic;
auto function() -> decltype(copy + 1) { return copy + 1; }

int main()
{
    static int local_static;
    thread_local int local_thread;
    int *unused = nullptr;
    extern int external;
    return local_static + local_thread + (unused == nullptr);
}
