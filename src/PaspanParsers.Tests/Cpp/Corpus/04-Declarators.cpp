// Declarators: pointers, references, arrays, functions, pointers to members, abstract declarators.

struct S
{
    int member;
    int method(int) const;
};

int *pointer;
int **pointer_to_pointer;
int *const const_pointer = nullptr;
const int *const *volatile mixed = nullptr;
int &lvalue_reference = *pointer;
int &&rvalue_reference = 1;
int array[10];
int matrix[2][3];
int unsized[] = { 1, 2, 3 };
int *array_of_pointers[4];
int (*pointer_to_array)[4];
int (&reference_to_array)[4] = *pointer_to_array;

int function(int, char *, double[]);
int (*function_pointer)(int);
int (*array_of_function_pointers[2])(int, int);
int (*(*function_returning_pointer)(int))[3];
void (*signal(int, void (*)(int)))(int);

int S::*member_pointer = &S::member;
int (S::*method_pointer)(int) const = &S::method;

int parenthesized_name(int (value));
int (grouped);

auto trailing(int x) -> int;
auto trailing_pointer() -> int (*)(int);
int noexcept_function() noexcept;
int noexcept_conditional() noexcept(sizeof(int) == 4);
void variadic(int count, ...);
void variadic_no_comma(int count...);
void only_variadic(...);
void no_parameters(void);
void default_arguments(int a = 1, int *b = nullptr, int (*c)(int) = nullptr);
void unnamed_parameters(int, int *, int (*)(int), int[]);
void array_parameter(int values[10], int grid[][4]);

int S::method(int x) const { return x + member; }

int unsigned_cast = sizeof(int (*)[3]) + sizeof(int *[3]) + sizeof(void (*)(int)) + sizeof(int S::*);
