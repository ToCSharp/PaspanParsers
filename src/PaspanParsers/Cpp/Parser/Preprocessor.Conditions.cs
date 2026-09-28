namespace PaspanParsers.Cpp;

// The conditions of #if and #elif ([cpp.cond]).
internal sealed partial class Preprocessor
{
    // The line of the directive being evaluated, for __LINE__
    private int _directiveLine;

    /// <summary>
    /// Evaluates the condition of <c>#if</c> or <c>#elif</c>: its macros are expanded, then it is an integer
    /// constant expression in which other names are 0. A condition that is not valid is false.
    /// </summary>
    private bool EvaluateCondition(ReadOnlySpan<byte> s, int hash, List<PpToken> tokens, int start)
    {
        _expansions = 0;
        if (tokens.Exists(t => t is { Kind: PpTokenKind.Identifier, Text: "__LINE__" }))
        {
            _directiveLine = s[..hash].Count((byte)'\n') + 1;
        }

        try
        {
            var expanded = Expand(tokens.GetRange(start, tokens.Count - start));
            var evaluator = new ConditionEvaluator(expanded);
            return evaluator.Evaluate() != 0;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    // ========================================
    // __has_include, __has_cpp_attribute, ...
    // ========================================

    /// <summary>
    /// The operators of conditions that are written like calls of function-like macros.
    /// </summary>
    private static readonly HashSet<string> BuiltinFunctions =
    [
        "__has_include", "__has_include_next", "__has_cpp_attribute", "__has_c_attribute", "__has_attribute",
        "__has_declspec_attribute", "__has_builtin", "__has_feature", "__has_extension", "__has_warning",
        "__is_identifier", "__building_module", "__has_embed",
    ];

    /// <summary>
    /// The standard attributes and the values of <c>__has_cpp_attribute</c> for them in C++23 (as clang 18 reports).
    /// </summary>
    private static readonly Dictionary<string, long> StandardAttributes = new(StringComparer.Ordinal)
    {
        ["carries_dependency"] = 200809,
        ["deprecated"] = 201309,
        ["fallthrough"] = 201603,
        ["likely"] = 201803,
        ["unlikely"] = 201803,
        ["maybe_unused"] = 201603,
        ["no_unique_address"] = 201803,
        ["nodiscard"] = 201907,
        ["noreturn"] = 200809,
    };

    /// <summary>
    /// Common GNU attributes (<c>__attribute__((name))</c>, <c>[[gnu::name]]</c>) for <c>__has_attribute</c>.
    /// </summary>
    private static readonly HashSet<string> GnuAttributes =
    [
        "alias", "aligned", "alloc_align", "alloc_size", "always_inline", "artificial", "assume_aligned", "cleanup",
        "cold", "const", "constructor", "deprecated", "destructor", "diagnose_if", "enable_if", "error", "fallthrough",
        "flatten", "format", "format_arg", "gnu_inline", "hot", "ifunc", "malloc", "may_alias", "mode", "no_sanitize",
        "no_sanitize_address", "no_sanitize_undefined", "noclone", "noinline", "noipa", "nonnull", "noreturn", "nothrow",
        "nodiscard", "optnone", "packed", "pure", "returns_nonnull", "returns_twice", "section", "sentinel", "target",
        "tls_model", "unavailable", "unused", "used", "vector_size", "visibility", "warn_unused_result", "warning", "weak",
        "weakref", "abi_tag", "init_priority", "no_unique_address", "lifetimebound", "trivial_abi", "noescape",
        "availability", "objc_boxable", "overloadable", "preferred_name", "uninitialized", "no_destroy", "exclude_from_explicit_instantiation",
        "internal_linkage", "maybe_unused", "nomerge", "noduplicate", "not_tail_called", "musttail", "likely", "unlikely",
        "annotate", "capability", "guarded_by", "acquire_capability", "release_capability", "requires_capability",
        "no_thread_safety_analysis", "scoped_lockable", "lockable", "consumable", "callback", "convergent", "leaf",
    ];

    /// <summary>
    /// Builtin functions and type traits that are not named <c>__builtin_*</c>, for <c>__has_builtin</c>.
    /// </summary>
    private static readonly HashSet<string> OtherBuiltins =
    [
        "__make_integer_seq", "__type_pack_element", "__underlying_type", "__remove_cv", "__remove_cvref", "__remove_const",
        "__remove_volatile", "__remove_reference_t", "__remove_pointer", "__remove_extent", "__remove_all_extents",
        "__add_lvalue_reference", "__add_rvalue_reference", "__add_pointer", "__decay", "__make_signed", "__make_unsigned",
        "__array_rank", "__array_extent", "__reference_binds_to_temporary", "__reference_constructs_from_temporary",
        "__reference_converts_from_temporary", "__datasizeof", "__arithmetic_fence",
    ];

    private long EvaluateBuiltinFunction(string name, List<PpToken> argument)
    {
        switch (name)
        {
            case "__has_include" or "__has_include_next":
            {
                if (argument.Count > 0 && argument[0].Kind != PpTokenKind.String && !argument[0].IsPunctuator("<"))
                {
                    argument = Expand(argument);
                }

                return HasInclude(argument) ? 1 : 0;
            }

            case "__has_cpp_attribute":
            {
                var (scope, attribute) = AttributeName(argument);
                if (scope == null)
                {
                    return StandardAttributes.GetValueOrDefault(attribute);
                }

                return scope is "gnu" or "clang" ? 1 : 0;
            }

            case "__has_attribute":
            {
                var (scope, attribute) = AttributeName(argument);
                return scope is null or "gnu" or "clang" && GnuAttributes.Contains(attribute) ? 1 : 0;
            }

            case "__has_builtin":
            {
                var builtin = Spell(argument, 0, argument.Count);
                return builtin.StartsWith("__builtin_", StringComparison.Ordinal) || builtin.StartsWith("__sync_", StringComparison.Ordinal)
                    || builtin.StartsWith("__atomic_", StringComparison.Ordinal) || builtin.StartsWith("__c11_atomic_", StringComparison.Ordinal)
                    || builtin.StartsWith("__is_", StringComparison.Ordinal) || builtin.StartsWith("__has_", StringComparison.Ordinal)
                    || OtherBuiltins.Contains(builtin) ? 1 : 0;
            }

            case "__has_feature" or "__has_extension":
            {
                var feature = Spell(argument, 0, argument.Count);
                if (feature.StartsWith("__", StringComparison.Ordinal) && feature.EndsWith("__", StringComparison.Ordinal) && feature.Length > 4)
                {
                    feature = feature[2..^2];
                }

                return feature.StartsWith("cxx_", StringComparison.Ordinal) || feature.StartsWith("attribute_", StringComparison.Ordinal)
                    || feature.StartsWith("is_", StringComparison.Ordinal) || feature.StartsWith("has_", StringComparison.Ordinal)
                    || feature is "modules" or "enumerator_attributes" or "tls" or "cxx_rtti" or "cxx_exceptions"
                    || (name == "__has_extension" && feature.StartsWith("c_", StringComparison.Ordinal)) ? 1 : 0;
            }

            case "__has_warning":
                return 1;

            case "__is_identifier":
                return argument is [{ Kind: PpTokenKind.Identifier } identifier] && !Lexer.Keywords.Contains(identifier.Text) ? 1 : 0;

            default:
                // __has_c_attribute, __has_declspec_attribute, __building_module, __has_embed
                return 0;
        }
    }

    /// <summary>
    /// The scope and name of an attribute (<c>gnu::always_inline</c>), without the underscores of the
    /// reserved spellings (<c>__nodiscard__</c>, <c>__gnu__</c>).
    /// </summary>
    private static (string Scope, string Name) AttributeName(List<PpToken> argument)
    {
        static string Normalize(string name) =>
            name.Length > 4 && name.StartsWith("__", StringComparison.Ordinal) && name.EndsWith("__", StringComparison.Ordinal) ? name[2..^2] : name;

        if (argument is [{ Kind: PpTokenKind.Identifier } scope, { Text: "::" }, { Kind: PpTokenKind.Identifier } name])
        {
            var normalizedScope = scope.Text == "_Clang" ? "clang" : Normalize(scope.Text);
            return (normalizedScope, Normalize(name.Text));
        }

        return argument is [{ Kind: PpTokenKind.Identifier } single] ? (null, Normalize(single.Text)) : (null, "");
    }

    /// <summary>
    /// <c>__has_include("header")</c> or <c>__has_include(&lt;header&gt;)</c>: the header is in the directory of
    /// the source (for a quoted name) or in one of the include directories of the options.
    /// </summary>
    private bool HasInclude(List<PpToken> argument)
    {
        string header;
        bool isAngled;
        if (argument is [{ Kind: PpTokenKind.String } quoted] && quoted.Text.Length >= 2 && quoted.Text[0] == '"')
        {
            header = quoted.Text[1..^1];
            isAngled = false;
        }
        else if (argument.Count >= 3 && argument[0].IsPunctuator("<") && argument[^1].IsPunctuator(">"))
        {
            header = string.Concat(argument.Skip(1).SkipLast(1).Select(t => t.Text));
            isAngled = true;
        }
        else
        {
            throw new FormatException("Expected a header name.");
        }

        if (header.Length == 0)
        {
            return false;
        }

        try
        {
            if (Path.IsPathRooted(header))
            {
                return File.Exists(header);
            }

            if (!isAngled && _options.SourceDirectory != null && File.Exists(Path.Combine(_options.SourceDirectory, header)))
            {
                return true;
            }

            return _options.IncludeDirectories.Any(directory => File.Exists(Path.Combine(directory, header)));
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    // ========================================
    // Evaluation
    // ========================================

    /// <summary>
    /// Evaluates a condition after macro expansion. Values are <c>intmax_t</c> or <c>uintmax_t</c>; names are 0
    /// (<c>true</c> is 1). Throws <see cref="FormatException"/> when the condition is not valid, and on
    /// division by zero where the operand is evaluated.
    /// </summary>
    private sealed class ConditionEvaluator(List<PpToken> tokens)
    {
        private int _index;

        private readonly record struct Value(long Number, bool IsUnsigned)
        {
            public bool IsTrue => Number != 0;
        }

        public long Evaluate()
        {
            if (tokens.Count == 0)
            {
                throw new FormatException("Expected a condition.");
            }

            var value = ParseComma(evaluated: true);
            if (_index != tokens.Count)
            {
                throw new FormatException($"Unexpected '{tokens[_index].Text}'.");
            }

            return value.Number;
        }

        private PpToken Current => _index < tokens.Count ? tokens[_index] : null;

        private bool TryEat(string punctuator)
        {
            if (Current?.IsPunctuator(punctuator) == true)
            {
                _index++;
                return true;
            }

            return false;
        }

        private Value ParseComma(bool evaluated)
        {
            var value = ParseConditional(evaluated);
            while (TryEat(","))
            {
                value = ParseConditional(evaluated);
            }

            return value;
        }

        private Value ParseConditional(bool evaluated)
        {
            EnsureSufficientStack();
            var condition = ParseBinary(1, evaluated);
            if (!TryEat("?"))
            {
                return condition;
            }

            var whenTrue = ParseComma(evaluated && condition.IsTrue);
            if (!TryEat(":"))
            {
                throw new FormatException("Expected ':'.");
            }

            var whenFalse = ParseConditional(evaluated && !condition.IsTrue);
            var isUnsigned = whenTrue.IsUnsigned || whenFalse.IsUnsigned;
            return new Value(condition.IsTrue ? whenTrue.Number : whenFalse.Number, isUnsigned);
        }

        private static int Precedence(PpToken token) => token is not { Kind: PpTokenKind.Punctuator } ? 0 : token.Text switch
        {
            "||" => 1,
            "&&" => 2,
            "|" => 3,
            "^" => 4,
            "&" => 5,
            "==" or "!=" => 6,
            "<" or ">" or "<=" or ">=" => 7,
            "<<" or ">>" => 8,
            "+" or "-" => 9,
            "*" or "/" or "%" => 10,
            _ => 0,
        };

        private Value ParseBinary(int minimum, bool evaluated)
        {
            var left = ParseUnary(evaluated);
            while (Current is { } token && Precedence(token) is var precedence && precedence >= minimum)
            {
                _index++;
                var @operator = token.Text;

                // The right operand of && and || is evaluated only when it decides the result
                var rightEvaluated = @operator switch
                {
                    "&&" => evaluated && left.IsTrue,
                    "||" => evaluated && !left.IsTrue,
                    _ => evaluated,
                };

                var right = ParseBinary(precedence + 1, rightEvaluated);
                left = Apply(@operator, left, right, evaluated);
            }

            return left;
        }

        private static Value Apply(string @operator, Value left, Value right, bool evaluated)
        {
            var isUnsigned = left.IsUnsigned || right.IsUnsigned;
            ulong l = (ulong)left.Number, r = (ulong)right.Number;
            long a = left.Number, b = right.Number;
            switch (@operator)
            {
                case "||":
                    return new Value(left.IsTrue || right.IsTrue ? 1 : 0, false);
                case "&&":
                    return new Value(left.IsTrue && right.IsTrue ? 1 : 0, false);
                case "==":
                    return new Value(a == b ? 1 : 0, false);
                case "!=":
                    return new Value(a != b ? 1 : 0, false);
                case "<":
                    return new Value((isUnsigned ? l < r : a < b) ? 1 : 0, false);
                case ">":
                    return new Value((isUnsigned ? l > r : a > b) ? 1 : 0, false);
                case "<=":
                    return new Value((isUnsigned ? l <= r : a <= b) ? 1 : 0, false);
                case ">=":
                    return new Value((isUnsigned ? l >= r : a >= b) ? 1 : 0, false);
                case "|":
                    return new Value(a | b, isUnsigned);
                case "^":
                    return new Value(a ^ b, isUnsigned);
                case "&":
                    return new Value(a & b, isUnsigned);
                case "<<":
                    return new Value(b is < 0 or >= 64 ? 0 : a << (int)b, left.IsUnsigned);
                case ">>":
                    return new Value(b is < 0 or >= 64 ? (left.IsUnsigned || a >= 0 ? 0 : -1) : left.IsUnsigned ? (long)(l >> (int)b) : a >> (int)b, left.IsUnsigned);
                case "+":
                    return new Value(unchecked(a + b), isUnsigned);
                case "-":
                    return new Value(unchecked(a - b), isUnsigned);
                case "*":
                    return new Value(unchecked(a * b), isUnsigned);
                case "/" or "%":
                    if (b == 0)
                    {
                        if (evaluated)
                        {
                            throw new FormatException("Division by zero.");
                        }

                        return new Value(0, isUnsigned);
                    }

                    if (isUnsigned)
                    {
                        return new Value((long)(@operator == "/" ? l / r : l % r), true);
                    }

                    if (a == long.MinValue && b == -1)
                    {
                        return new Value(@operator == "/" ? long.MinValue : 0, false);
                    }

                    return new Value(@operator == "/" ? a / b : a % b, false);
                default:
                    throw new FormatException($"Unexpected '{@operator}'.");
            }
        }

        private Value ParseUnary(bool evaluated)
        {
            EnsureSufficientStack();
            var token = Current ?? throw new FormatException("Unexpected end of the condition.");
            if (token.Kind == PpTokenKind.Punctuator)
            {
                switch (token.Text)
                {
                    case "+":
                        _index++;
                        return ParseUnary(evaluated);
                    case "-":
                    {
                        _index++;
                        var operand = ParseUnary(evaluated);
                        return operand with { Number = unchecked(-operand.Number) };
                    }

                    case "~":
                    {
                        _index++;
                        var operand = ParseUnary(evaluated);
                        return operand with { Number = ~operand.Number };
                    }

                    case "!":
                    {
                        _index++;
                        var operand = ParseUnary(evaluated);
                        return new Value(operand.IsTrue ? 0 : 1, false);
                    }

                    case "(":
                    {
                        _index++;
                        var value = ParseComma(evaluated);
                        if (!TryEat(")"))
                        {
                            throw new FormatException("Expected ')'.");
                        }

                        return value;
                    }
                }

                throw new FormatException($"Unexpected '{token.Text}'.");
            }

            _index++;
            switch (token.Kind)
            {
                case PpTokenKind.Identifier:
                    return new Value(token.Text == "true" ? 1 : 0, false);

                case PpTokenKind.Number:
                {
                    var literal = Literals.Number(token.Text);
                    if (literal.Kind != LiteralKind.Integer || literal.Value is not ulong value || literal.UserDefinedSuffix != null)
                    {
                        throw new FormatException($"'{token.Text}' is not an integer.");
                    }

                    var isUnsigned = literal.Suffix?.Contains('u', StringComparison.OrdinalIgnoreCase) == true || value > long.MaxValue;
                    return new Value((long)value, isUnsigned);
                }

                case PpTokenKind.Character:
                {
                    var literal = Literals.Character(token.Text);
                    if (literal.Value is not long value || literal.UserDefinedSuffix != null)
                    {
                        throw new FormatException($"'{token.Text}' has no value.");
                    }

                    return literal.Encoding switch
                    {
                        // char is signed, as on the targets of clang and GCC for x86
                        CharacterEncoding.Ordinary when value <= 0xFF => new Value((sbyte)(byte)value, false),
                        CharacterEncoding.Ordinary or CharacterEncoding.Wide => new Value(value, false),
                        _ => new Value(value, true),
                    };
                }

                default:
                    throw new FormatException($"Unexpected '{token.Text}'.");
            }
        }
    }
}
