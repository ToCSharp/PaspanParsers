// Functions, variables and statements with fundamental types only.

int counter = 0;
const long limit = 100L, step = 2;
static unsigned int flags;

int square(int x)
{
    return x * x;
}

double average(double a, double b)
{
    return (a + b) / 2.0;
}

bool is_even(int value, int divisor = 2)
{
    return value % divisor == 0;
}

void tick()
{
    counter++;
    ++counter;
    counter += step;
    counter = counter << 1 >> 1;
}

int clamp(int value, int low, int high)
{
    if (value < low)
        return low;
    else if (value > high)
    {
        return high;
    }

    return value;
}

int sum_to(int n)
{
    int total = 0, i = 0;
    while (i <= n)
    {
        total = total + i;
        i = i + 1;
    }

    return total;
}

char grade(int score)
{
    return score >= 90 ? 'A' : score >= 75 ? 'B' : 'C';
}

int main()
{
    int a = square(3) + square(4);
    bool ok = is_even(a) && !is_even(a + 1) || a != 25;
    double mean = average(1.5, 2.5e1);
    unsigned long mask = ~0UL & 0xFFu | 0b1010 ^ 017;
    const bool unused = nullptr == nullptr;
    ;
    tick();
    if (ok)
        a = -a;
    return clamp(sum_to(a), -1, 1) + grade(a) - mean * 0 + (true ? 0 : 1);
}
