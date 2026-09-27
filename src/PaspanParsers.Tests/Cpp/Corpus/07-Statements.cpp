// Statements: selection, iteration, jumps, labels, exceptions, structured bindings.

#include <initializer_list>

struct Range
{
    int data[3] = { 1, 2, 3 };
    const int *begin() const { return data; }
    const int *end() const { return data + 3; }
};

struct Tuple { int first; double second; };

int next();

int statements(int n)
{
    int total = 0;

    if (n > 0) total++;
    if (n > 1) { total += 2; } else { total -= 2; }
    if (int m = n * 2; m > 10) total += m;
    if (int k = next()) total += k;
    if constexpr (sizeof(int) == 4) total += 4; else total -= 4;
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
    case 3:
    case 4:
        total++;
        break;
    default:
        break;
    }

    switch (int v = next(); v) { case 1: return v; default:; }

    while (n-- > 0) total++;
    while (int k = next()) { if (k > 3) break; else continue; }
    do total--; while (total > 100);
    do { total++; } while (false);

    for (int i = 0; i < n; ++i) total += i;
    for (int i = 0, j = n; i < j; ++i, --j) {}
    for (;;) { break; }
    for (total = 0; total < 3;) total++;

    Range range;
    for (int value : range) total += value;
    for (const auto &value : range) total += value;
    for (auto &&value : { 1, 2, 3 }) total += value;
    for (Range copy = range; auto value : copy) total += value;

    Tuple tuple{ 1, 2.0 };
    auto [first, second] = tuple;
    auto &[ref_first, ref_second] = tuple;
    const auto &&[a, b] = Tuple{ 3, 4.0 };
    total += first + ref_first + a + static_cast<int>(second + ref_second + b);

    goto end;
    total = -1;
end:
    ;
label: {
        total++;
    }

    try
    {
        if (total > 1000) throw total;
    }
    catch (int error)
    {
        total = error;
    }
    catch (const Range &)
    {
        throw;
    }
    catch (...)
    {
        return -1;
    }

    {
        ;
        {}
    }

    return total;
}

void function_try_block(int x)
try
{
    if (x) throw x;
}
catch (int)
{
}

void returns_void() { return; }
void returns_expression() { return returns_void(); }
