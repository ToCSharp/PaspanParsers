// Declarators: pointers, references, arrays, functions, pointers to members, abstract declarators.

struct S;

int value = 1;
int *pointer = &value;
int **pointer_to_pointer = &pointer;
int *const const_pointer = nullptr;
const int *const *volatile mixed = nullptr;
int &lvalue_reference = *pointer;
int &&rvalue_reference = 1;
int array[10];
int matrix[2][3];
int sized_by_expression[2 * 3 + 1];
extern int unsized[];
int *array_of_pointers[4];
int (*pointer_to_array)[4];
int (&reference_to_array)[4] = *pointer_to_array;

int function(int, char *, double[]);
int (*function_pointer)(int);
int (*array_of_function_pointers[2])(int, int);
int (*(*function_returning_pointer)(int))[3];
void (*signal(int, void (*)(int)))(int);
int (*returns_array_pointer(int))[3];

int S::*member_pointer = nullptr;
int (S::*method_pointer)(int) const = nullptr;
int (S::*rvalue_method_pointer)() && = nullptr;
const int S::*const const_member_pointer = nullptr;
int S::**pointer_to_member_pointer = nullptr;

int parenthesized_name(int (value));
int (grouped);
int ((double_grouped));
int *(parenthesized_pointer), (*pointer_in_parentheses);

auto trailing(int x) -> int;
auto trailing_pointer() -> int (*)(int);
auto trailing_array_pointer() -> int (*)[3];
int noexcept_function() noexcept;
int noexcept_true() noexcept(true);
int noexcept_expression() noexcept(1 + 1 == 2);
void variadic(int count, ...);
void variadic_no_comma(int count...);
void only_variadic(...);
void no_parameters(void);
void default_arguments(int a = 1, int *b = nullptr, int (*c)(int) = nullptr);
void unnamed_parameters(int, int *, int (*)(int), int[], int (int), int &&, int (&)[3]);
void array_parameter(int values[10], int grid[][4]);
void function_parameter(int callback(int), int (*pointer)(int));
unsigned long long operator""_km(unsigned long long);

int defined_function(int a, int b)
{
    int local_array[3];
    int *local_pointer = local_array;
    int (*local_array_pointer)[3] = &local_array;
    int (*local_function_pointer)(int) = function_pointer;
    int &local_reference = a;
    int local_function(int);
    return a + b + *local_pointer + local_reference;
}

int (*resolve(int selector))(int)
{
    return function_pointer;
}

auto defined_trailing(int x) -> int (*)[4]
{
    return pointer_to_array;
}
