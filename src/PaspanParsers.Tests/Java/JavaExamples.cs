namespace PaspanParsers.Tests.Java;

/// <summary>
/// Collection of Java code examples demonstrating various language features
/// </summary>
public static class JavaExamples
{
    // ========================================
    // Basic Examples
    // ========================================

    public const string SimpleClass = @"
package com.example;

public class Person {
    private String name;
    private int age;
    
    public Person(String name, int age) {
        this.name = name;
        this.age = age;
    }
    
    public String getName() {
        return name;
    }
    
    public int getAge() {
        return age;
    }
}
";

    public const string SimpleInterface = @"
package com.example;

public interface Comparable<T> {
    int compareTo(T other);
    boolean equals(Object obj);
}
";

    public const string SimpleEnum = @"
package com.example;

public enum Color {
    RED,
    GREEN,
    BLUE
}
";

    // ========================================
    // Generics Examples
    // ========================================

    public const string GenericClass = @"
package com.example;

public class Box<T extends Number> {
    private T value;
    
    public Box(T value) {
        this.value = value;
    }
    
    public T getValue() {
        return value;
    }
    
    public void setValue(T value) {
        this.value = value;
    }
}
";

    public const string MultipleTypeParameters = @"
package com.example;

public class Pair<K, V> {
    private K key;
    private V value;
    
    public Pair(K key, V value) {
        this.key = key;
        this.value = value;
    }
    
    public K getKey() {
        return key;
    }
    
    public V getValue() {
        return value;
    }
}
";

    public const string WildcardGenerics = @"
package com.example;

import java.util.List;

public class Utils {
    public static void printList(List<?> list) {
    }
    
    public static void addNumbers(List<? super Integer> list) {
    }
    
    public static double sumNumbers(List<? extends Number> list) {
        return 0.0;
    }
}
";

    // ========================================
    // Modern Java Features
    // ========================================

    public const string RecordExample = @"
package com.example;

public record Point(int x, int y) {
    public double distanceFromOrigin() {
        return 0.0;
    }
    
    public Point translate(int dx, int dy) {
        return new Point(x + dx, y + dy);
    }
}
";

    public const string SealedClass = @"
package com.example;

public sealed class Shape permits Circle, Rectangle, Triangle {
    public abstract double area();
}

final class Circle extends Shape {
    private final double radius;
    
    public Circle(double radius) {
        this.radius = radius;
    }
    
    public double area() {
        return 3.14 * radius * radius;
    }
}

final class Rectangle extends Shape {
    private final double width;
    private final double height;
    
    public Rectangle(double width, double height) {
        this.width = width;
        this.height = height;
    }
    
    public double area() {
        return width * height;
    }
}

final class Triangle extends Shape {
    private final double base;
    private final double height;
    
    public Triangle(double base, double height) {
        this.base = base;
        this.height = height;
    }
    
    public double area() {
        return 0.5 * base * height;
    }
}
";

    public const string VarExample = @"
package com.example;

public class VarDemo {
    public void demonstrateVar() {
        var x = 10;
        var name = ""John"";
        var list = new ArrayList<String>();
    }
}
";

    // ========================================
    // Annotations Examples
    // ========================================

    public const string AnnotatedClass = @"
package com.example;

@Deprecated
@SuppressWarnings(""all"")
public class LegacyCode {
    @Override
    public String toString() {
        return """";
    }
    
    @Deprecated
    public void oldMethod() {
    }
}
";

    public const string CustomAnnotation = @"
package com.example;

public @interface Author {
    String name();
    String date();
    String[] contributors() default {};
}
";

    // ========================================
    // Inheritance Examples
    // ========================================

    public const string InheritanceExample = @"
package com.example;

public class Animal {
    protected String name;
    
    public Animal(String name) {
        this.name = name;
    }
    
    public void makeSound() {
    }
}

class Dog extends Animal {
    public Dog(String name) {
        super(name);
    }
    
    @Override
    public void makeSound() {
    }
}
";

    public const string InterfaceImplementation = @"
package com.example;

public class ArrayList<E> implements List<E>, Cloneable {
    private E[] elements;
    
    public int size() {
        return 0;
    }
    
    public boolean isEmpty() {
        return true;
    }
}
";

    // ========================================
    // Complex Examples
    // ========================================

    public const string Calculator = @"
package com.example;

public class Calculator {
    public int add(int a, int b) {
        return a + b;
    }
    
    public int subtract(int a, int b) {
        return a - b;
    }
    
    public int multiply(int a, int b) {
        return a * b;
    }
    
    public int divide(int a, int b) {
        if (b == 0) {
            throw new ArithmeticException();
        }
        return a / b;
    }
}
";

    public const string EnumWithMethods = @"
package com.example;

public enum Operation {
    PLUS,
    MINUS,
    MULTIPLY,
    DIVIDE;
    
    public double apply(double x, double y) {
        return 0.0;
    }
}
";

    public const string GenericMethod = @"
package com.example;

public class Utils {
    public static <T> T identity(T value) {
        return value;
    }
    
    public static <T extends Comparable<T>> T max(T a, T b) {
        return a;
    }
}
";

    // ========================================
    // Arrays and Collections
    // ========================================

    public const string ArrayExample = @"
package com.example;

public class ArrayDemo {
    private int[] numbers;
    private String[][] matrix;
    
    public void processArray(int[] arr) {
        for (int i = 0; i < arr.length; i = i + 1) {
            int value = arr[i];
        }
    }
}
";

    // ========================================
    // Static and Final
    // ========================================

    public const string StaticExample = @"
package com.example;

public class Constants {
    public static final int MAX_SIZE = 100;
    public static final double PI = 3.14159;
    public static final String APP_NAME = ""MyApp"";
    
    private static int counter = 0;
    
    public static int getNextId() {
        counter = counter + 1;
        return counter;
    }
}
";

    // ========================================
    // Nested Classes
    // ========================================

    public const string NestedClass = @"
package com.example;

public class Outer {
    private int value;
    
    public class Inner {
        public void accessOuter() {
            int x = value;
        }
    }
    
    public static class StaticNested {
        private int data;
        
        public int getData() {
            return data;
        }
    }
}
";

    // ========================================
    // Exception Handling (simplified)
    // ========================================

    public const string ExceptionExample = @"
package com.example;

public class FileProcessor {
    public String readFile(String path) {
        if (path == null) {
            throw new IllegalArgumentException();
        }
        return """";
    }
}
";

    // ========================================
    // Abstract Classes
    // ========================================

    public const string AbstractClassExample = @"
package com.example;

public abstract class Vehicle {
    protected String brand;
    protected int year;
    
    public Vehicle(String brand, int year) {
        this.brand = brand;
        this.year = year;
    }
    
    public abstract void start();
    
    public abstract void stop();
    
    public String getBrand() {
        return brand;
    }
}
";

    // ========================================
    // Multiple Modifiers
    // ========================================

    public const string ModifiersExample = @"
package com.example;

public abstract class BaseClass {
    protected static final int CONSTANT = 42;
    private volatile int counter;
    public transient String tempData;
    
    public synchronized void increment() {
        counter = counter + 1;
    }
    
    protected abstract void process();
    
    public static void staticMethod() {
    }
}
";

    // ========================================
    // Package and Imports
    // ========================================

    public const string ImportsExample = @"
package com.example.app;

import java.util.List;
import java.util.ArrayList;
import java.util.Map;
import java.util.HashMap;
import static java.lang.Math.PI;
import static java.lang.Math.sqrt;

public class Demo {
    private List<String> items;
    private Map<String, Integer> counts;
}
";

    // ========================================
    // Varargs
    // ========================================

    public const string VarargsExample = @"
package com.example;

public class VarargsDemo {
    public void printAll(String... messages) {
    }
    
    public int sum(int... numbers) {
        int total = 0;
        for (int i = 0; i < numbers.length; i = i + 1) {
            total = total + numbers[i];
        }
        return total;
    }
}
";

    // ========================================
    // Final Parameters
    // ========================================

    public const string FinalParametersExample = @"
package com.example;

public class ImmutableDemo {
    public void process(final int value, final String name) {
    }
    
    public String format(final Object obj) {
        return """";
    }
}
";

    // ========================================
    // Generic Interface
    // ========================================

    public const string GenericInterface = @"
package com.example;

public interface Repository<T, ID> {
    T findById(ID id);
    void save(T entity);
    void delete(ID id);
    List<T> findAll();
}
";

    // ========================================
    // Record with Interface
    // ========================================

    public const string RecordWithInterface = @"
package com.example;

public record Employee(String name, int id, String department) implements Comparable<Employee> {
    public int compareTo(Employee other) {
        return 0;
    }
}
";

    // ========================================
    // Complex Generic Bounds
    // ========================================

    public const string ComplexGenerics = @"
package com.example;

public class SortedList<T extends Number & Comparable<T>> {
    private List<T> items;
    
    public void add(T item) {
    }
    
    public T getMax() {
        return null;
    }
}
";

    // ========================================
    // Enum with Constructor
    // ========================================

    public const string EnumWithConstructor = @"
package com.example;

public enum Size {
    SMALL,
    MEDIUM,
    LARGE;
    
    public int getValue() {
        return 0;
    }
}
";

    // ========================================
    // Multiple Interfaces
    // ========================================

    public const string MultipleInterfaces = @"
package com.example;

public class DataProcessor implements Runnable, Comparable<DataProcessor>, Cloneable {
    public void run() {
    }
    
    public int compareTo(DataProcessor other) {
        return 0;
    }
}
";

    // ========================================
    // Real-World Example: Simple Data Model
    // ========================================

    public const string RealWorldExample = @"
package com.example.model;

import java.util.List;
import java.util.ArrayList;

public class User {
    private String username;
    private String email;
    private boolean active;
    private List<String> roles;
    
    public User(String username, String email) {
        this.username = username;
        this.email = email;
        this.active = true;
        this.roles = new ArrayList<String>();
    }
    
    public String getUsername() {
        return username;
    }
    
    public void setUsername(String username) {
        this.username = username;
    }
    
    public String getEmail() {
        return email;
    }
    
    public void setEmail(String email) {
        this.email = email;
    }
    
    public boolean isActive() {
        return active;
    }
    
    public void setActive(boolean active) {
        this.active = active;
    }
    
    public List<String> getRoles() {
        return roles;
    }
    
    public void addRole(String role) {
        roles.add(role);
    }
}
";
}

