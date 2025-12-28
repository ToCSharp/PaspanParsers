
# Python Parser Implementation

This directory contains a Python parser and code generator implementation using the Paspan parsing library.

## Overview

The Python parser supports Python 3.6 through 3.12 syntax, including:
- Classes, functions, and methods
- Control flow (if, while, for, try-except, with, match)
- Comprehensions (list, dict, set, generator)
- Decorators
- Type hints
- F-strings
- Async/await
- Walrus operator (:=)
- Pattern matching (Python 3.10+)
- And more...

## Project Structure

```
Python/
├── PythonGrammarSpecification.txt  # Formal grammar definition (EBNF)
├── README.md                        # This file
├── PythonAst.cs                     # AST node definitions (~100+ classes)
├── PythonParser.cs                  # Parser implementation using Paspan
├── PythonParserTests.cs             # Unit tests for the parser
├── PythonWriter.cs                  # Code generator (AST → Python code)
├── PythonWriterTests.cs             # Unit tests for the code generator
└── PythonExamples.cs                # Example Python code snippets
```

## Features

### Supported Python Versions

The parser targets Python 3.6+ with support for:

#### Python 3.6
- ✅ F-strings
- ✅ Variable annotations
- ✅ Underscores in numeric literals
- ✅ Async comprehensions

#### Python 3.7
- ✅ Postponed evaluation of annotations
- ✅ Data classes (as regular classes)

#### Python 3.8
- ✅ Walrus operator (`:=`)
- ✅ Positional-only parameters (`/`)
- ✅ F-string `=` specifier

#### Python 3.9
- ✅ Dictionary merge operators (`|`, `|=`)
- ✅ Type hinting generics (`list[int]`)

#### Python 3.10
- ✅ Match statement (structural pattern matching)
- ✅ Parenthesized context managers
- ✅ Union type operator (`|`)

#### Python 3.11+
- ⚠️ Exception groups (partially supported)
- ⚠️ Task groups (not implemented)

### Language Features

#### 1. **Module Structure**
- Import statements (import, from...import)
- Module-level code
- `__all__` exports

#### 2. **Type System**
- Dynamic typing
- Type hints (PEP 484)
- Optional types
- Union types
- Generic types
- Type aliases

#### 3. **Statements**

**Simple Statements:**
- Expression statements
- Assignment statements
- Augmented assignment (+=, -=, etc.)
- Annotated assignments (x: int = 5)
- Assert statements
- Pass statements
- Del statements
- Return statements
- Yield statements
- Raise statements
- Break/continue statements
- Import statements
- Global/nonlocal statements

**Compound Statements:**
- If/elif/else
- While/else
- For/else
- Try/except/else/finally
- With statements (context managers)
- Match/case (Python 3.10+)
- Function definitions
- Class definitions
- Async function definitions

#### 4. **Expressions**

**Primary Expressions:**
- Literals (int, float, complex, string, bytes, None, True, False)
- Names (identifiers)
- Attribute access (obj.attr)
- Subscription (list[0])
- Slicing (list[1:10:2])
- Calls (func(args))
- Comprehensions
- Generator expressions

**Operators:**
- Arithmetic: +, -, *, /, //, %, **, @
- Comparison: ==, !=, <, >, <=, >=
- Logical: and, or, not
- Bitwise: &, |, ^, ~, <<, >>
- Membership: in, not in
- Identity: is, is not
- Walrus: :=
- Ternary: x if cond else y

**Special Forms:**
- Lambda expressions
- Conditional expressions
- Await expressions
- Yield expressions

#### 5. **Collections**
- Lists: `[1, 2, 3]`
- Tuples: `(1, 2, 3)`
- Sets: `{1, 2, 3}`
- Dicts: `{'key': 'value'}`
- List comprehensions: `[x*2 for x in range(10) if x % 2 == 0]`
- Dict comprehensions: `{k: v for k, v in items}`
- Set comprehensions: `{x for x in range(10)}`
- Generator expressions: `(x for x in range(10))`

#### 6. **Functions**
- Function definitions
- Parameters (positional, keyword, default, *args, **kwargs)
- Positional-only parameters (/)
- Keyword-only parameters (*)
- Type hints for parameters and return values
- Decorators
- Nested functions
- Closures
- Lambda expressions

#### 7. **Classes**
- Class definitions
- Inheritance (single and multiple)
- Methods (instance, class, static)
- Properties
- Decorators
- Special methods (__init__, __str__, etc.)
- Metaclasses (basic support)

#### 8. **Decorators**
- Function decorators
- Class decorators
- Built-in decorators (@property, @staticmethod, @classmethod)
- Decorator with arguments
- Multiple decorators

#### 9. **Async/Await**
- Async function definitions
- Await expressions
- Async comprehensions
- Async context managers (async with)
- Async iterators (async for)

#### 10. **Pattern Matching (Python 3.10+)**
- Match statement
- Literal patterns
- Capture patterns
- Wildcard patterns
- Value patterns
- Sequence patterns
- Mapping patterns
- Class patterns
- OR patterns
- AS patterns
- Guards

#### 11. **Special Features**
- F-strings with expressions
- Walrus operator (`:=`)
- Unpacking (`*args`, `**kwargs`)
- Extended unpacking (`a, *b, c = items`)
- Ellipsis (`...`)
- Context managers (`with` statement)

## AST Structure

The Abstract Syntax Tree (AST) consists of over 100 classes organized into:

### Base Types
- `IPythonNode` - Base interface for all AST nodes
- `Statement` - Base class for statements
- `Expression` - Base class for expressions
- `Pattern` - Base class for match patterns

### Top-Level Structures
- `Module` - Root node representing a Python file
- `ImportStatement` - Import declarations
- `FunctionDef` - Function definitions
- `ClassDef` - Class definitions

### Statements
- `ExpressionStatement`
- `AssignmentStatement`
- `AugmentedAssignment`
- `AnnotatedAssignment`
- `IfStatement`
- `WhileStatement`
- `ForStatement`
- `TryStatement`
- `WithStatement`
- `MatchStatement`
- `ReturnStatement`
- `YieldStatement`
- `RaiseStatement`
- `AssertStatement`
- `PassStatement`
- `BreakStatement`
- `ContinueStatement`
- `DeleteStatement`
- `GlobalStatement`
- `NonlocalStatement`

### Expressions
- `NameExpression`
- `LiteralExpression`
- `BinaryOperation`
- `UnaryOperation`
- `CompareOperation`
- `BooleanOperation`
- `LambdaExpression`
- `ConditionalExpression`
- `CallExpression`
- `AttributeExpression`
- `SubscriptExpression`
- `SliceExpression`
- `ListExpression`
- `TupleExpression`
- `SetExpression`
- `DictExpression`
- `ListComprehension`
- `SetComprehension`
- `DictComprehension`
- `GeneratorExpression`
- `AwaitExpression`
- `YieldExpression`
- `StarredExpression`
- `NamedExpression` (walrus operator)

### Type Hints
- `TypeHint`
- `NameType`
- `SubscriptType`
- `UnionType`
- `OptionalType`
- `ListType`
- `TupleType`
- `DictType`
- `CallableType`

### Patterns (Python 3.10+)
- `MatchValue`
- `MatchSingleton`
- `MatchSequence`
- `MatchMapping`
- `MatchClass`
- `MatchStar`
- `MatchAs`
- `MatchOr`

### Other
- `Decorator`
- `Argument`
- `Keyword`
- `Comprehension`
- `ExceptHandler`
- `WithItem`
- `Alias` (for imports)

## Usage

### Parsing Python Code

```csharp
using Paspan.Tests.Python;

var parser = new PythonParser();
var sourceCode = @"
def greet(name: str) -> str:
    return f'Hello, {name}!'

class Person:
    def __init__(self, name: str, age: int):
        self.name = name
        self.age = age
";

var result = parser.ParseModule(sourceCode);
if (result.Success)
{
    Module module = result.Value;
    Console.WriteLine($"Parsed {module.Body.Count} top-level statements");
    
    // Access AST nodes
    foreach (var stmt in module.Body)
    {
        if (stmt is FunctionDef func)
        {
            Console.WriteLine($"Function: {func.Name}");
        }
        else if (stmt is ClassDef cls)
        {
            Console.WriteLine($"Class: {cls.Name}");
        }
    }
}
else
{
    Console.WriteLine($"Parse error: {result.Error}");
}
```

### Generating Python Code

```csharp
using Paspan.Tests.Python;

// Create AST nodes
var funcDef = new FunctionDef(
    name: "add",
    args: new Arguments(
        args: new[]
        {
            new Arg("a", typeAnnotation: new NameType("int")),
            new Arg("b", typeAnnotation: new NameType("int"))
        }
    ),
    body: new[]
    {
        new ReturnStatement(
            new BinaryOperation(
                left: new NameExpression("a"),
                op: BinaryOperator.Add,
                right: new NameExpression("b")
            )
        )
    },
    returns: new NameType("int")
);

// Generate code
var writer = new PythonWriter();
writer.WriteFunctionDef(funcDef);
var code = writer.GetResult();

Console.WriteLine(code);
// Output:
// def add(a: int, b: int) -> int:
//     return a + b
```

### Writing a Module

```csharp
var module = new Module(
    body: new Statement[]
    {
        new ImportStatement(
            names: new[] { new Alias("sys") }
        ),
        new FunctionDef(
            name: "main",
            args: new Arguments(),
            body: new[]
            {
                new ExpressionStatement(
                    new CallExpression(
                        func: new AttributeExpression(
                            value: new NameExpression("sys"),
                            attr: "exit"
                        ),
                        args: new[] { new LiteralExpression(0) }
                    )
                )
            }
        ),
        new IfStatement(
            test: new CompareOperation(
                left: new NameExpression("__name__"),
                ops: new[] { CompareOperator.Eq },
                comparators: new[] { new LiteralExpression("__main__") }
            ),
            body: new[]
            {
                new ExpressionStatement(
                    new CallExpression(
                        func: new NameExpression("main"),
                        args: Array.Empty<Expression>()
                    )
                )
            }
        )
    }
);

var writer = new PythonWriter();
writer.WriteModule(module);
Console.WriteLine(writer.GetResult());
```

## Parser Implementation Details

The parser is implemented using Paspan's combinator library:

1. **Lexical Analysis**: Tokenizes Python source into keywords, operators, identifiers, literals
2. **Indentation Handling**: Tracks INDENT/DEDENT tokens for Python's significant whitespace
3. **Expression Parsing**: Implements operator precedence climbing
4. **Statement Parsing**: Handles simple and compound statements
5. **Error Recovery**: Basic error messages and recovery strategies

### Key Parser Components

- `Keywords`: Reserved words in Python
- `Operators`: All Python operators and delimiters
- `Literals`: Numbers, strings, f-strings, bytes
- `Identifiers`: Variable and function names
- `Expressions`: Recursive expression parser with precedence
- `Statements`: Statement parsers for all Python statement types
- `Types`: Type hint parser for annotations
- `Patterns`: Pattern matching for match statements

## Code Writer Implementation

The `PythonWriter` class generates Python code from AST nodes:

1. **Indentation Management**: Tracks and writes proper indentation (4 spaces by default)
2. **Statement Writing**: Converts statement nodes to Python syntax
3. **Expression Writing**: Handles operator precedence and parenthesization
4. **Special Formatting**: F-strings, comprehensions, decorators, etc.

## Limitations and Known Issues

### Not Fully Implemented
1. ⚠️ Some advanced pattern matching features
2. ⚠️ Exception groups (Python 3.11+)
3. ⚠️ Type parameter syntax (Python 3.12+)
4. ⚠️ Complex string prefix combinations
5. ⚠️ Some edge cases in f-string parsing
6. ⚠️ Metaclasses (partial support)

### Intentional Simplifications
1. No encoding declaration parsing
2. Simplified error recovery
3. No comment preservation in AST
4. Line numbers and positions are simplified
5. Some rare syntactic forms may not be supported

## Testing

The implementation includes comprehensive test suites:

### PythonParserTests.cs
Tests for parsing various Python constructs:
- Basic statements (pass, break, continue, return)
- Assignments (simple, augmented, annotated)
- Control flow (if, while, for, match)
- Functions (def, async def, lambda, decorators)
- Classes (class, inheritance, decorators)
- Expressions (operators, calls, comprehensions)
- Imports (import, from...import, aliases)
- Exception handling (try, except, finally)
- Context managers (with, async with)
- Type hints (parameters, returns, variables)
- F-strings
- Pattern matching

### PythonWriterTests.cs
Tests for code generation:
- Statement generation
- Expression formatting
- Indentation handling
- Complex nested structures
- Round-trip tests (parse → generate → parse)

### PythonExamples.cs
Real-world Python code examples:
- Simple scripts
- Classes and inheritance
- Decorators
- Async functions
- Comprehensions
- Type hints
- Pattern matching
- And more...

## Comparison with Other Implementations

### vs C# Parser
- Python uses indentation instead of braces
- Dynamic typing with optional type hints
- First-class functions and closures
- Different module/package system
- Comprehensions as core language feature
- Different approach to async/await

### vs Java Parser
- No semicolons or braces for blocks
- Duck typing vs static typing
- Multiple inheritance
- No interfaces (uses abstract classes and protocols)
- Simpler syntax for many constructs
- More flexible method overloading

## Examples

### Example 1: Simple Function
```python
def fibonacci(n: int) -> int:
    if n <= 1:
        return n
    return fibonacci(n-1) + fibonacci(n-2)
```

### Example 2: Class with Type Hints
```python
from typing import List, Optional

class Stack:
    def __init__(self) -> None:
        self._items: List[int] = []
    
    def push(self, item: int) -> None:
        self._items.append(item)
    
    def pop(self) -> Optional[int]:
        return self._items.pop() if self._items else None
```

### Example 3: List Comprehension
```python
squares = [x**2 for x in range(10) if x % 2 == 0]
```

### Example 4: Decorator
```python
def timer(func):
    def wrapper(*args, **kwargs):
        start = time.time()
        result = func(*args, **kwargs)
        end = time.time()
        print(f"{func.__name__} took {end - start} seconds")
        return result
    return wrapper

@timer
def slow_function():
    time.sleep(1)
```

### Example 5: Async Function
```python
async def fetch_data(url: str) -> dict:
    async with aiohttp.ClientSession() as session:
        async with session.get(url) as response:
            return await response.json()
```

### Example 6: Pattern Matching
```python
match command:
    case "quit":
        print("Quitting...")
    case "help":
        print("Available commands: quit, help, status")
    case ["set", var, value]:
        print(f"Setting {var} to {value}")
    case _:
        print("Unknown command")
```

### Example 7: F-string
```python
name = "World"
age = 42
message = f"Hello, {name}! You are {age} years old."
```

### Example 8: Context Manager
```python
with open("file.txt", "r") as f:
    content = f.read()
    print(content)
```

### Example 9: Walrus Operator
```python
if (n := len(items)) > 10:
    print(f"List has {n} items")
```

### Example 10: Multiple Inheritance
```python
class Animal:
    def speak(self):
        pass

class Mammal(Animal):
    def give_birth(self):
        pass

class Dog(Mammal):
    def speak(self):
        return "Woof!"
```

## References

- [Python Language Reference](https://docs.python.org/3/reference/)
- [Python Grammar](https://docs.python.org/3/reference/grammar.html)
- [PEP 8 - Style Guide](https://www.python.org/dev/peps/pep-0008/)
- [PEP 484 - Type Hints](https://www.python.org/dev/peps/pep-0484/)
- [PEP 498 - F-strings](https://www.python.org/dev/peps/pep-0498/)
- [PEP 572 - Walrus Operator](https://www.python.org/dev/peps/pep-0572/)
- [PEP 634 - Structural Pattern Matching](https://www.python.org/dev/peps/pep-0634/)
- [Abstract Syntax Trees (AST)](https://docs.python.org/3/library/ast.html)

## Contributing

This is an educational project demonstrating parser construction with Paspan. 
Contributions, bug reports, and suggestions are welcome!

## License

Same license as the parent Paspan project.

