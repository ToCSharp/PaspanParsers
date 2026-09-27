// Coroutines: co_await, co_yield and co_return.

#include <coroutine>

struct Task
{
    struct promise_type
    {
        int value = 0;
        Task get_return_object() { return Task{ std::coroutine_handle<promise_type>::from_promise(*this) }; }
        std::suspend_never initial_suspend() noexcept { return {}; }
        std::suspend_always final_suspend() noexcept { return {}; }
        std::suspend_always yield_value(int v) { value = v; return {}; }
        void return_value(int v) { value = v; }
        void unhandled_exception() {}
    };

    std::coroutine_handle<promise_type> handle;
};

struct Awaitable
{
    bool await_ready() const noexcept { return true; }
    void await_suspend(std::coroutine_handle<>) noexcept {}
    int await_resume() const noexcept { return 42; }
};

Task generator(int n)
{
    for (int i = 0; i < n; ++i)
        co_yield i;
    int awaited = co_await Awaitable{};
    co_return awaited + n;
}

Task nested()
{
    co_await std::suspend_never{};
    co_return co_await Awaitable{} + 1;
}
