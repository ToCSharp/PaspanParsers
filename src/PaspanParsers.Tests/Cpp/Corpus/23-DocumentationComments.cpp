// Documentation comments, checked against the comments clang attaches to declarations: ///, //!, /** */,
// /*! */, groups, trailing comments ///< and what separates a comment from its declaration.

/// A variable.
/// The second line of its comment.
int documented_variable;

/// Separated from the next comment by a blank line: not part of the group.

/// Only this group documents the variable.
int after_blank_line;

/// An ordinary comment between the comment and the declaration does not matter.
// ordinary
int after_ordinary_comment;

/// A directive between the comment and the declaration does.
#define DOCUMENTATION_MARKER 1
int after_directive;

/// An ordinary comment with a semicolon; between them does too.
// see a; b
int after_semicolon;

/** A block comment.
 * With a second line.
 */
int block_documented;

/*! A Qt-style block comment. */
int qt_documented;

//! A Qt-style line comment.
int qt_line_documented;

/// Kinds can be mixed in one group,
/** a block, */ //! and a Qt line.
int mixed_group;

////
int four_slashes;

/**/
int empty_block;

///
int empty_line;

int trailing_variable; ///< A trailing comment.
int first_of_two, ///< The first.
    second_of_two; ///< The second.
int trailing_group; ///< A trailing comment
                    ///< that continues.

/// Before the declaration, but the trailing comment wins.
int trailing_wins; ///< The trailing comment.

int not_trailing; /// Not a trailing comment: it documents the next declaration.
int documented_by_previous_line;

/// Documents both declarators.
int shared_first, shared_second;

/// Documents the first declarator only: the braces separate it from the second.
int braced_first{1}, braced_second;

/// A function.
/// \param value The value.
/// \return Its double.
int twice(int value) { return 2 * value; }

/// A declared function.
void declared_function(); ///< Functions have no trailing comments.

/// A typedef.
typedef int Integer; ///< Neither have typedefs.

/// An alias.
using Number = int;

/// An enumeration.
enum Color
{
    Red,   ///< Red.
    /// Green.
    Green,
    Blue /**< Blue. */,
    Alpha = 4, //!< Transparency.
};

/// A scoped enumeration.
enum class Direction : unsigned char
{
    /// Up.
    Up,
    /// Down.
    Down,
};

/// A namespace.
namespace documented
{
    /// A class.
    class Point
    {
    public:
        /// The constructor.
        Point(int x, int y) : x(x), y(y) {}

        /// A member function.
        int sum() const
        {
            /// A local variable.
            int result = x + y; ///< With a trailing comment.
            return result;
        }

        /// A static member.
        static int count; ///< Counted.

        int x; ///< The x coordinate.
        int y; ///< The y coordinate.

        /// A bit-field.
        unsigned flags : 3;

    private:
        /// Documents the access specifier and the member after it.
    protected:
        int hidden;
    };

    /// Defined outside the class.
    int Point::count = 0;
}

/// A class template.
template <typename T>
struct Box
{
    /// The value.
    T value;
};

template <typename T>
/// Between the template head and the declaration.
T unbox(Box<T> box) { return box.value; }

/// A variable template.
template <typename T>
constexpr T zero = T(); ///< Trailing comments of variable templates.

/// A concept.
template <typename T>
concept Boxable = sizeof(T) > 0;

/// An explicit specialization.
template <>
struct Box<void>
{
};

/// A static assertion.
static_assert(sizeof(int) >= 2);

/// A using-directive.
using namespace documented;

/// A linkage specification.
extern "C"
{
    /// Inside it.
    int c_function(int);
}

/// An unnamed class and a variable of it.
struct { int q; } unnamed_class_variable; /// Documents the next declaration, with this line.
/// This line.
int after_unnamed_class;

struct Documented
{
    /// Merged across the members.
    int a; int b; ///< Trailing b.
};

/// Directive in the middle of the group
#if 1
/// Documents the declaration after the conditional directive.
int after_conditional;
#endif

struct Pair { int a; int b; };

void statements()
{
    /// A local variable.
    int local = 1; ///< With a trailing comment.
    for (/** A loop variable. */ int i = 0; i < local; i++)
    {
    }

    /// A structured binding.
    auto [first, second] = Pair{1, 2};
    (void)first;
    (void)second;
}
