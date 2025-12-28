namespace PaspanParsers.Tests.Python;

/// <summary>
/// Collection of Python code examples demonstrating various language features
/// </summary>
public static class PythonExamples
{
    // ========================================
    // Basic Examples
    // ========================================

    public const string SimpleFunction = @"
def greet(name):
    return ""Hello, "" + name
";

    public const string FunctionWithTypeHints = @"
def add(a: int, b: int) -> int:
    return a + b
";

    public const string SimpleClass = @"
class Person:
    def __init__(self, name, age):
        self.name = name
        self.age = age
    
    def greet(self):
        return ""Hello, I'm "" + self.name
";

    public const string ClassWithInheritance = @"
class Animal:
    def speak(self):
        pass

class Dog(Animal):
    def speak(self):
        return ""Woof!""
";

    // ========================================
    // Control Flow
    // ========================================

    public const string IfStatement = @"
def check_sign(x):
    if x > 0:
        return ""positive""
    elif x < 0:
        return ""negative""
    else:
        return ""zero""
";

    public const string ForLoop = @"
def sum_list(numbers):
    total = 0
    for num in numbers:
        total = total + num
    return total
";

    public const string WhileLoop = @"
def countdown(n):
    while n > 0:
        print(n)
        n = n - 1
    print(""Done!"")
";

    // ========================================
    // Collections
    // ========================================

    public const string ListExample = @"
fruits = [""apple"", ""banana"", ""cherry""]
numbers = [1, 2, 3, 4, 5]
empty_list = []
";

    public const string DictExample = @"
person = {
    ""name"": ""John"",
    ""age"": 30,
    ""city"": ""New York""
}
";

    public const string SetExample = @"
unique_numbers = {1, 2, 3, 4, 5}
colors = {""red"", ""green"", ""blue""}
";

    public const string TupleExample = @"
coordinates = (10, 20)
point = (x, y, z)
";

    // ========================================
    // Comprehensions
    // ========================================

    public const string ListComprehension = @"
squares = [x**2 for x in range(10)]
evens = [x for x in range(20) if x % 2 == 0]
";

    public const string DictComprehension = @"
squares_dict = {x: x**2 for x in range(5)}
";

    public const string SetComprehension = @"
unique_lengths = {len(word) for word in words}
";

    // ========================================
    // Lambda and Higher-Order Functions
    // ========================================

    public const string LambdaExample = @"
add = lambda x, y: x + y
square = lambda x: x**2
";

    public const string MapFilterExample = @"
numbers = [1, 2, 3, 4, 5]
doubled = map(lambda x: x * 2, numbers)
evens = filter(lambda x: x % 2 == 0, numbers)
";

    // ========================================
    // Decorators
    // ========================================

    public const string DecoratorExample = @"
@staticmethod
def helper():
    pass

@property
def name(self):
    return self._name
";

    // ========================================
    // Exception Handling
    // ========================================

    public const string TryExceptExample = @"
def safe_divide(a, b):
    try:
        return a / b
    except ZeroDivisionError:
        return None
    finally:
        print(""Operation completed"")
";

    // ========================================
    // Context Managers
    // ========================================

    public const string WithStatement = @"
def read_file(filename):
    with open(filename, ""r"") as f:
        content = f.read()
    return content
";

    // ========================================
    // Multiple Inheritance
    // ========================================

    public const string MultipleInheritance = @"
class Animal:
    def breathe(self):
        pass

class Mammal(Animal):
    def feed_young(self):
        pass

class Dog(Mammal):
    def bark(self):
        return ""Woof!""
";

    // ========================================
    // Class Methods and Static Methods
    // ========================================

    public const string ClassMethods = @"
class MyClass:
    class_var = 0
    
    def __init__(self, value):
        self.value = value
    
    @classmethod
    def from_string(cls, s):
        return cls(int(s))
    
    @staticmethod
    def helper():
        return 42
";

    // ========================================
    // Properties
    // ========================================

    public const string PropertyExample = @"
class Circle:
    def __init__(self, radius):
        self._radius = radius
    
    @property
    def radius(self):
        return self._radius
    
    @radius.setter
    def radius(self, value):
        if value < 0:
            raise ValueError(""Radius cannot be negative"")
        self._radius = value
";

    // ========================================
    // Generator
    // ========================================

    public const string GeneratorExample = @"
def count_up_to(n):
    i = 1
    while i <= n:
        yield i
        i = i + 1
";

    // ========================================
    // Async/Await
    // ========================================

    public const string AsyncExample = @"
async def fetch_data(url):
    response = await http.get(url)
    return response.json()
";

    // ========================================
    // Type Hints
    // ========================================

    public const string TypeHintsExample = @"
from typing import List, Dict, Optional

def process_items(items: List[str]) -> Dict[str, int]:
    result: Dict[str, int] = {}
    for item in items:
        result[item] = len(item)
    return result

def find_user(user_id: int) -> Optional[str]:
    if user_id > 0:
        return ""User""
    return None
";

    // ========================================
    // Dataclass-like Pattern
    // ========================================

    public const string DataClassPattern = @"
class Point:
    def __init__(self, x: int, y: int):
        self.x: int = x
        self.y: int = y
    
    def distance_from_origin(self) -> float:
        return (self.x**2 + self.y**2)**0.5
";

    // ========================================
    // Iterator Protocol
    // ========================================

    public const string IteratorExample = @"
class Counter:
    def __init__(self, start, end):
        self.current = start
        self.end = end
    
    def __iter__(self):
        return self
    
    def __next__(self):
        if self.current >= self.end:
            raise StopIteration
        self.current = self.current + 1
        return self.current - 1
";

    // ========================================
    // Context Manager Protocol
    // ========================================

    public const string ContextManagerExample = @"
class ManagedResource:
    def __enter__(self):
        print(""Acquiring resource"")
        return self
    
    def __exit__(self, exc_type, exc_val, exc_tb):
        print(""Releasing resource"")
        return False
";

    // ========================================
    // Magic Methods
    // ========================================

    public const string MagicMethodsExample = @"
class Vector:
    def __init__(self, x, y):
        self.x = x
        self.y = y
    
    def __add__(self, other):
        return Vector(self.x + other.x, self.y + other.y)
    
    def __str__(self):
        return ""Vector({}, {})"".format(self.x, self.y)
    
    def __repr__(self):
        return ""Vector({}, {})"".format(self.x, self.y)
    
    def __eq__(self, other):
        return self.x == other.x and self.y == other.y
";

    // ========================================
    // Unpacking
    // ========================================

    public const string UnpackingExample = @"
a, b = 1, 2
x, y, z = [1, 2, 3]
first, *rest = [1, 2, 3, 4, 5]
*beginning, last = [1, 2, 3, 4, 5]
";

    // ========================================
    // Conditional Expression
    // ========================================

    public const string ConditionalExpression = @"
result = ""positive"" if x > 0 else ""non-positive""
max_value = a if a > b else b
";

    // ========================================
    // Multiple Assignment
    // ========================================

    public const string MultipleAssignment = @"
a = b = c = 0
x, y = 10, 20
";

    // ========================================
    // F-Strings (Python 3.6+)
    // ========================================

    public const string FStringExample = @"
name = ""Alice""
age = 30
message = f""Hello, {name}! You are {age} years old.""
";

    // ========================================
    // Walrus Operator (Python 3.8+)
    // ========================================

    public const string WalrusOperator = @"
if (n := len(items)) > 10:
    print(f""List has {n} items"")
";

    // ========================================
    // Match Statement (Python 3.10+)
    // ========================================

    public const string MatchStatement = @"
def handle_command(command):
    match command:
        case ""quit"":
            return ""Quitting...""
        case ""help"":
            return ""Help message""
        case _:
            return ""Unknown command""
";

    // ========================================
    // Real-World Example: Simple Web Server Handler
    // ========================================

    public const string RealWorldExample = @"
from typing import Dict, Optional

class RequestHandler:
    def __init__(self):
        self.routes: Dict[str, callable] = {}
    
    def route(self, path: str):
        def decorator(func):
            self.routes[path] = func
            return func
        return decorator
    
    def handle(self, path: str) -> Optional[str]:
        handler = self.routes.get(path)
        if handler:
            return handler()
        return None

handler = RequestHandler()

@handler.route(""/home"")
def home():
    return ""Welcome to the home page""

@handler.route(""/about"")
def about():
    return ""About us""
";
}

