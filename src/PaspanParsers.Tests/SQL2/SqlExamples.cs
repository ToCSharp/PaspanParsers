namespace PaspanParsers.Tests.SQL2;

/// <summary>
/// Example SQL queries for testing and demonstration.
/// </summary>
public static class SqlExamples
{
    public const string SimpleSelect = "SELECT * FROM users";

    public const string SelectWithWhere = @"
        SELECT id, name, email 
        FROM users 
        WHERE age >= 18";

    public const string SelectWithJoin = @"
        SELECT u.name, o.total
        FROM users u
        INNER JOIN orders o ON u.id = o.user_id
        WHERE o.status = 'completed'";

    public const string SelectWithGroupBy = @"
        SELECT category, COUNT(*), AVG(price)
        FROM products
        GROUP BY category
        HAVING AVG(price) > 100";

    public const string ComplexSelect = @"
        SELECT 
            u.name,
            u.email,
            COUNT(o.id) AS order_count,
            SUM(o.total) AS total_spent
        FROM users u
        INNER JOIN orders o ON u.id = o.user_id
        WHERE o.status = 'completed'
          AND o.created_at >= '2024-01-01'
        GROUP BY u.id, u.name, u.email
        HAVING COUNT(o.id) >= 5
        ORDER BY total_spent DESC
        LIMIT 10";

    public const string InsertStatement = @"
        INSERT INTO users (name, email, age)
        VALUES ('John Doe', 'john@example.com', 25)";

    public const string UpdateStatement = @"
        UPDATE products
        SET price = price * 1.1
        WHERE category = 'electronics'";

    public const string DeleteStatement = @"
        DELETE FROM users
        WHERE last_login < '2023-01-01'";

    public const string CreateTable = @"
        CREATE TABLE users (
            id INT PRIMARY KEY AUTO_INCREMENT,
            name VARCHAR(100) NOT NULL,
            email VARCHAR(255) UNIQUE NOT NULL,
            age INT,
            created_at DATETIME DEFAULT NOW()
        )";

    public const string CreateTableWithConstraints = @"
        CREATE TABLE orders (
            id INT PRIMARY KEY,
            user_id INT NOT NULL,
            product_id INT NOT NULL,
            total DECIMAL(10, 2),
            FOREIGN KEY (user_id) REFERENCES users(id),
            FOREIGN KEY (product_id) REFERENCES products(id)
        )";
}

