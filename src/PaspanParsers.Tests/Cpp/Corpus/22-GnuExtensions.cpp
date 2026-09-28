// GNU extensions that clang accepts without macros: attributes, asm declarations and labels, __restrict.

__attribute__((noinline)) int not_inlined(int value) { return value; }
int __attribute__((unused)) unused_first, __attribute__((unused)) unused_second;
static __attribute__((unused)) int in_specifiers;
int after_declarator __attribute__((unused)) = 1;
void declared_with_attributes(int) __attribute__((deprecated("old"), nothrow));
int renamed asm("renamed_symbol");
int renamed_function(int) __asm__("renamed_function_symbol");

struct __attribute__((packed)) Packed
{
    char tag;
    int value __attribute__((aligned(4)));
};

struct Aligned { alignas(16) float values[4]; } __attribute__((may_alias));

asm(".globl gnu_marker");
__asm__(".set gnu_marker, 0");

int *__restrict restricted_pointer;
void copy(char *__restrict__ destination, const char *__restrict source, int length);

void assembly()
{
    asm("nop");
    __asm__ volatile("" ::: "memory");
    int value = 0;
    __asm__ __volatile__("" : "+r"(value));
    (void)value;
}
