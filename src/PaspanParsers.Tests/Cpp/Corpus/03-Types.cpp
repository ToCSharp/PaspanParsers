// Type specifiers: fundamental types, cv-qualifiers, elaborated and named types, typedefs, decltype, auto.

struct Point;
class Shape;
union Value;

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
__int128 wide;
unsigned __int128 unsigned_wide;

const int ci = 1;
int const ic = 2;
volatile int vi;
const volatile int cvi = 3;
volatile const unsigned int vcu = 4;

struct Point *p1;
Point *p2 = p1;
const Point *cp = nullptr;
class Shape *shape;
union Value *value;
extern struct Point point_object;
::Point *global_point;
struct Undeclared *first_use;
Undeclared *second_use = first_use;

typedef int Integer;
typedef unsigned long Size, *SizePointer;
typedef struct Point PointType;
typedef void Function(int);
typedef int (*Callback)(int);
typedef int Matrix[3][3];
typedef const Integer ConstInteger;
int typedef late_typedef;

Integer typed = 1;
Size size = 2;
SizePointer size_pointer = &size;
PointType *point_type = p1;
Callback callback = nullptr;
Function *function_pointer = nullptr;
Matrix identity;
ConstInteger constant = 3;
::Integer qualified = 4;
Integer const trailing_const = 5;
late_typedef from_late_typedef = 6;

decltype(ci) copy = ci;
decltype((copy)) reference = copy;
decltype(auto) deduced = copy;
decltype(copy + 1L) sum = copy + 1L;
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
    Integer local_integer = 1;
    Integer *integer_pointer = &local_integer;
    Integer &integer_reference = local_integer;
    Integer (parenthesized) = 2;
    Point *local_point = p1;
    typedef double Real;
    Real real = 2;
    const Real *real_pointer = &real;
    Callback local_callback = callback;
    return local_static + local_thread + (unused == nullptr) + *integer_pointer + integer_reference + parenthesized;
}
