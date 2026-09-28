// Attributes and alignment specifiers.

[[nodiscard]] int must_use();
[[nodiscard("reason")]] int must_use_with_reason();
[[deprecated]] void old_function();
[[deprecated("use new_function")]] void older_function();
[[noreturn]] void never_returns();
[[maybe_unused]] static int unused_variable;
[[gnu::always_inline]] inline void inlined() {}
[[using gnu: hot, noinline]] void temperature();
[[nodiscard, deprecated]] int both();
[[]] int empty_attribute;

struct [[nodiscard]] Result { int code; };
class [[deprecated]] OldClass {};
enum class [[nodiscard]] Status { Ok };
union [[maybe_unused]] Unused { int a; };
namespace [[deprecated]] old_namespace { }

struct alignas(16) Aligned { char data[16]; };
struct alignas(double) AlignedAsType { char c; };
alignas(8) int aligned_variable;
alignas(Aligned) char buffer[32];

template <typename... T>
struct alignas(T...) AlignedPack {};

int function([[maybe_unused]] int parameter, int other [[maybe_unused]])
{
    [[maybe_unused]] int local = 1;
    int array [[maybe_unused]] [2];
    [[likely]] if (other > 0) { return 1; }
    if (parameter) [[unlikely]] { return 2; }
    switch (parameter)
    {
    [[likely]] case 1:
        [[fallthrough]];
    case 2:
        break;
    }

    [[assume(other >= 0)]];
    return 0;
}

int trailing_function [[deprecated]] ();
int (*attributed_pointer [[maybe_unused]])() = nullptr;
