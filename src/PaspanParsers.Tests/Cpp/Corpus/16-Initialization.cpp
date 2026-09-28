// Initialization: direct, copy, list, aggregate, designated, value and default initialization.

#include <initializer_list>

struct Point { int x, y; };
struct Line { Point from, to; int width = 1; };
struct Settings { int width; int height; bool fullscreen; };
struct Constructed
{
    Constructed() {}
    Constructed(int, int) {}
    Constructed(std::initializer_list<int>) {}
};

int copy_init = 1;
int direct_init(2);
int list_init{ 3 };
int copy_list_init = { 4 };
int value_init{};
int array_init[3] = { 1, 2, 3 };
int array_list[]{ 4, 5 };
int nested_array[2][2] = { { 1, 2 }, { 3, 4 } };
int brace_elision[2][2] = { 1, 2, 3, 4 };
char text[] = "text";
char text_braces[] = { "text" };
Point point = { 1, 2 };
Point point_list{ 3, 4 };
Line line = { { 0, 0 }, { 1, 1 } };
Line trailing_comma = { { 0, 0 }, { 1, 1 }, 2, };
Settings designated = { .width = 800, .height = 600, .fullscreen = false };
Settings partly_designated{ .width = 1024 };
Line nested_designated{ .from = { .x = 1 }, .to{ 2, 3 } };
Point parenthesized_aggregate(5, 6);
Constructed default_constructed;
Constructed from_pair(1, 2);
Constructed from_list{ 1, 2, 3 };
Constructed empty_list{};
Constructed copy_from_list = { 1 };
Constructed temporary = Constructed(1, 2);
Constructed temporary_list = Constructed{ 1, 2 };
auto deduced_list = { 1, 2 };
int *new_array = new int[2]{ 1, 2 };

Point make_point() { return { 7, 8 }; }
Point make_default() { return {}; }

void initialize(int a)
{
    int local{ a };
    int uninitialized;
    static int zeroed;
    Point p{};
    Point q = Point{ 1, 2 };
    Point r = Point();
    int *pointer{};
    const int &reference{ a };
    auto lambda_init = [x{ a }] { return x; };
    int array[2]{};
    uninitialized = local + zeroed + p.x + q.y + r.x + (pointer == nullptr) + reference + lambda_init() + array[0];
}
