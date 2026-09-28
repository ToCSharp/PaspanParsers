// Statements: selection, iteration, jumps, labels, exceptions, structured bindings, attributes.

#include <initializer_list>

int next();
typedef int Integer;

int statements(int n)
{
    int total = 0;

    if (n > 0) total++;
    if (n > 1) { total += 2; } else { total -= 2; }
    if (n > 2) total++; else if (n > 3) total--; else {}
    if (int m = n * 2; m > 10) total += m;
    if (int k = next()) total += k;
    if (Integer k{ next() }) total += k;
    if (int (parenthesized) = next()) total += parenthesized;
    if (total++; total > 5) total = 5;
    if (; total) {}
    if constexpr (sizeof(int) == 4) total += 4; else total -= 4;
    if consteval { total += 1; } else { total -= 1; }
    if !consteval { total += 1; }

    switch (n)
    {
    case 0:
        total = 0;
        break;
    case 1:
    case 2:
    {
        int local = n;
        total += local;
        [[fallthrough]];
    }
    case 3 ... 5:
        total++;
        break;
    [[likely]] case 6:
        total--;
        [[fallthrough]];
    default:
        break;
    }

    switch (int v = next(); v) { case 1: return v; default:; }
    switch (n) { default: }
    switch (n) total++;

    while (n-- > 0) total++;
    while (int k = next()) { if (k > 3) break; else continue; }
    do total--; while (total > 100);
    do { total++; } while (false);

    for (int i = 0; i < n; ++i) total += i;
    for (int i = 0, j = n; i < j; ++i, --j) {}
    for (;;) { break; }
    for (total = 0; total < 3;) total++;
    for (; int k = next();) total += k;
    for (auto f = [] { return 1; }; total < f(); ) total++;

    int values[3] = { 1, 2, 3 };
    for (int value : values) total += value;
    for (const auto &value : values) total += value;
    for (auto &&value : { 1, 2, 3 }) total += value;
    for (int copy[2] = { 4, 5 }; auto value : copy) total += value;
    for ([[maybe_unused]] int value : values) {}

    int pairs[2][2] = { { 1, 2 }, { 3, 4 } };
    for (auto [left, right] : pairs) total += left * right;
    for (const auto &[left, right] : pairs) total += left - right;
    auto [first, second, third] = values;
    auto &[ref_first, ref_second, ref_third] = values;
    const auto &&[a, b] = static_cast<int (&&)[2]>(pairs[0]);
    total += first + second + third + ref_first + ref_second + ref_third + a + b;

    [[maybe_unused]] int unused = 0;
    static_assert(sizeof(long) >= 4);
    [[likely]] total++;
    [[unlikely]] { total--; }

    goto end;
    total = -1;
end:
    ;
label: {
        total++;
    }
[[maybe_unused]] unused_label:
    total++;

    try
    {
        if (total > 1000) throw total;
    }
    catch (int error)
    {
        total = error;
    }
    catch (const char *)
    {
        throw;
    }
    catch (int (&array)[2])
    {
        total = array[0];
    }
    catch (...)
    {
        return -1;
    }

    try { try { throw 1.0; } catch (double) { throw; } } catch (...) {}

    {
        ;
        {}
    }

    return total;

finish:
}

void returns_void() { return; }
void returns_expression() { return returns_void(); }
int returns_list() { return { 1 }; }
