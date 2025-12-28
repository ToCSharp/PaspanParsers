-- SQL EXAMPLES - Comprehensive Reference
-- This file contains examples of various SQL constructs for testing and reference
-- ================================================================================

-- BASIC SELECT STATEMENTS
-- ================================================================================

-- Simple select
SELECT * FROM users;

-- Select with specific columns
SELECT id, name, email FROM users;

-- Select with aliases
SELECT 
    u.id AS user_id,
    u.name AS full_name,
    u.email AS contact_email
FROM users AS u;

-- DISTINCT and ALL
SELECT DISTINCT city FROM customers;
SELECT ALL product_name FROM products;

-- WHERE CLAUSES
-- ================================================================================

-- Simple conditions
SELECT * FROM users WHERE age > 18;
SELECT * FROM products WHERE price <= 100.00;
SELECT * FROM orders WHERE status = 'completed';
SELECT * FROM items WHERE category <> 'archived';

-- Logical operators
SELECT * FROM users 
WHERE age >= 18 AND country = 'USA';

SELECT * FROM products 
WHERE category = 'electronics' OR category = 'appliances';

SELECT * FROM orders 
WHERE NOT status = 'cancelled';

-- BETWEEN
SELECT * FROM products 
WHERE price BETWEEN 10.00 AND 50.00;

SELECT * FROM events 
WHERE event_date NOT BETWEEN '2024-01-01' AND '2024-12-31';

-- IN operator
SELECT * FROM users 
WHERE country IN ('USA', 'Canada', 'Mexico');

SELECT * FROM products 
WHERE id NOT IN (SELECT product_id FROM discontinued_products);

-- LIKE pattern matching
SELECT * FROM users WHERE name LIKE 'John%';
SELECT * FROM emails WHERE address LIKE '%@gmail.com';
SELECT * FROM products WHERE code LIKE 'PRD-____-2024';
SELECT * FROM items WHERE description NOT LIKE '%discontinued%';

-- IS NULL
SELECT * FROM users WHERE deleted_at IS NULL;
SELECT * FROM orders WHERE shipped_date IS NOT NULL;

-- JOIN OPERATIONS
-- ================================================================================

-- INNER JOIN
SELECT u.name, o.order_id, o.total
FROM users u
INNER JOIN orders o ON u.id = o.user_id;

-- Multiple joins
SELECT 
    u.name,
    o.order_id,
    oi.product_id,
    p.product_name
FROM users u
INNER JOIN orders o ON u.id = o.user_id
INNER JOIN order_items oi ON o.id = oi.order_id
INNER JOIN products p ON oi.product_id = p.id;

-- LEFT JOIN
SELECT u.name, o.order_id
FROM users u
LEFT JOIN orders o ON u.id = o.user_id;

-- RIGHT JOIN
SELECT u.name, o.order_id
FROM orders o
RIGHT JOIN users u ON o.user_id = u.id;

-- CROSS JOIN
SELECT u.name, p.product_name
FROM users u
CROSS JOIN products p;

-- Self join
SELECT 
    e1.name AS employee,
    e2.name AS manager
FROM employees e1
LEFT JOIN employees e2 ON e1.manager_id = e2.id;

-- AGGREGATE FUNCTIONS
-- ================================================================================

-- Basic aggregates
SELECT COUNT(*) FROM users;
SELECT COUNT(DISTINCT country) FROM users;
SELECT SUM(total) FROM orders;
SELECT AVG(price) FROM products;
SELECT MAX(created_at) FROM orders;
SELECT MIN(price) FROM products;

-- Aggregates with WHERE
SELECT AVG(price) 
FROM products 
WHERE category = 'electronics';

-- GROUP BY
-- ================================================================================

-- Simple grouping
SELECT country, COUNT(*) as user_count
FROM users
GROUP BY country;

-- Multiple columns
SELECT country, city, COUNT(*) as count
FROM users
GROUP BY country, city;

-- GROUP BY with aggregates
SELECT 
    category,
    COUNT(*) as product_count,
    AVG(price) as avg_price,
    MIN(price) as min_price,
    MAX(price) as max_price
FROM products
GROUP BY category;

-- HAVING clause
SELECT category, AVG(price) as avg_price
FROM products
GROUP BY category
HAVING AVG(price) > 100;

-- GROUP BY with JOIN
SELECT 
    u.country,
    COUNT(o.id) as order_count,
    SUM(o.total) as total_sales
FROM users u
LEFT JOIN orders o ON u.id = o.user_id
GROUP BY u.country
HAVING COUNT(o.id) > 10;

-- ORDER BY
-- ================================================================================

-- Simple ordering
SELECT * FROM users ORDER BY name;
SELECT * FROM products ORDER BY price DESC;

-- Multiple columns
SELECT * FROM users 
ORDER BY country ASC, city ASC, name DESC;

-- Order by expression
SELECT name, price, price * 0.9 as discounted_price
FROM products
ORDER BY price * 0.9 DESC;

-- NULLS FIRST/LAST
SELECT * FROM users ORDER BY deleted_at NULLS FIRST;
SELECT * FROM products ORDER BY discount NULLS LAST;

-- LIMIT and OFFSET
-- ================================================================================

-- Top N records
SELECT * FROM products ORDER BY price DESC LIMIT 10;

-- Pagination
SELECT * FROM users ORDER BY id LIMIT 20 OFFSET 40;

-- FETCH FIRST (SQL standard)
SELECT * FROM products FETCH FIRST 5 ROWS ONLY;

-- SUBQUERIES
-- ================================================================================

-- Scalar subquery
SELECT name, price,
    (SELECT AVG(price) FROM products) as avg_price
FROM products;

-- IN with subquery
SELECT * FROM users
WHERE id IN (SELECT user_id FROM orders WHERE total > 1000);

-- EXISTS
SELECT * FROM users u
WHERE EXISTS (
    SELECT 1 FROM orders o 
    WHERE o.user_id = u.id AND o.status = 'completed'
);

-- NOT EXISTS
SELECT * FROM products p
WHERE NOT EXISTS (
    SELECT 1 FROM order_items oi
    WHERE oi.product_id = p.id
);

-- Subquery in FROM
SELECT subquery.category, subquery.avg_price
FROM (
    SELECT category, AVG(price) as avg_price
    FROM products
    GROUP BY category
) AS subquery
WHERE subquery.avg_price > 50;

-- Correlated subquery
SELECT u.name,
    (SELECT COUNT(*) FROM orders o WHERE o.user_id = u.id) as order_count
FROM users u;

-- UNION, INTERSECT, EXCEPT
-- ================================================================================

-- UNION (removes duplicates)
SELECT name FROM customers
UNION
SELECT name FROM suppliers;

-- UNION ALL (keeps duplicates)
SELECT product_id FROM order_items WHERE order_date > '2024-01-01'
UNION ALL
SELECT product_id FROM wishlist WHERE added_date > '2024-01-01';

-- INTERSECT
SELECT user_id FROM orders WHERE created_at > '2024-01-01'
INTERSECT
SELECT user_id FROM reviews WHERE created_at > '2024-01-01';

-- EXCEPT
SELECT user_id FROM all_users
EXCEPT
SELECT user_id FROM banned_users;

-- COMMON TABLE EXPRESSIONS (CTEs)
-- ================================================================================

-- Simple CTE
WITH top_products AS (
    SELECT * FROM products
    WHERE price > 100
    ORDER BY price DESC
    LIMIT 10
)
SELECT * FROM top_products;

-- Multiple CTEs
WITH 
    active_users AS (
        SELECT * FROM users WHERE status = 'active'
    ),
    recent_orders AS (
        SELECT * FROM orders WHERE created_at > CURRENT_DATE - INTERVAL '30 days'
    )
SELECT u.name, COUNT(o.id) as order_count
FROM active_users u
LEFT JOIN recent_orders o ON u.id = o.user_id
GROUP BY u.name;

-- Recursive CTE (organizational hierarchy)
WITH RECURSIVE employee_hierarchy AS (
    -- Base case
    SELECT id, name, manager_id, 1 as level
    FROM employees
    WHERE manager_id IS NULL
    
    UNION ALL
    
    -- Recursive case
    SELECT e.id, e.name, e.manager_id, eh.level + 1
    FROM employees e
    INNER JOIN employee_hierarchy eh ON e.manager_id = eh.id
)
SELECT * FROM employee_hierarchy;

-- WINDOW FUNCTIONS
-- ================================================================================

-- ROW_NUMBER
SELECT 
    name,
    price,
    ROW_NUMBER() OVER (ORDER BY price DESC) as row_num
FROM products;

-- RANK and DENSE_RANK
SELECT 
    name,
    price,
    RANK() OVER (ORDER BY price DESC) as rank,
    DENSE_RANK() OVER (ORDER BY price DESC) as dense_rank
FROM products;

-- PARTITION BY
SELECT 
    category,
    name,
    price,
    ROW_NUMBER() OVER (PARTITION BY category ORDER BY price DESC) as rank_in_category
FROM products;

-- Window functions with aggregates
SELECT 
    name,
    price,
    AVG(price) OVER () as avg_price,
    price - AVG(price) OVER () as diff_from_avg
FROM products;

-- PARTITION BY with aggregates
SELECT 
    category,
    name,
    price,
    AVG(price) OVER (PARTITION BY category) as category_avg_price
FROM products;

-- LAG and LEAD
SELECT 
    date,
    revenue,
    LAG(revenue, 1) OVER (ORDER BY date) as prev_day_revenue,
    LEAD(revenue, 1) OVER (ORDER BY date) as next_day_revenue
FROM daily_sales;

-- Frame specification
SELECT 
    date,
    revenue,
    SUM(revenue) OVER (
        ORDER BY date 
        ROWS BETWEEN 2 PRECEDING AND CURRENT ROW
    ) as rolling_3day_sum
FROM daily_sales;

-- CASE EXPRESSIONS
-- ================================================================================

-- Simple CASE
SELECT 
    name,
    CASE category
        WHEN 'electronics' THEN 'Tech'
        WHEN 'clothing' THEN 'Fashion'
        ELSE 'Other'
    END as category_group
FROM products;

-- Searched CASE
SELECT 
    name,
    price,
    CASE 
        WHEN price < 10 THEN 'Budget'
        WHEN price >= 10 AND price < 100 THEN 'Mid-range'
        WHEN price >= 100 THEN 'Premium'
        ELSE 'Unknown'
    END as price_tier
FROM products;

-- CASE in ORDER BY
SELECT * FROM products
ORDER BY 
    CASE 
        WHEN category = 'featured' THEN 1
        WHEN category = 'new' THEN 2
        ELSE 3
    END,
    price DESC;

-- COALESCE
SELECT 
    name,
    COALESCE(phone, mobile, email, 'No contact') as contact
FROM users;

-- NULLIF
SELECT 
    product_name,
    NULLIF(discount_price, regular_price) as actual_discount
FROM products;

-- INSERT STATEMENTS
-- ================================================================================

-- Simple insert
INSERT INTO users (name, email, age)
VALUES ('John Doe', 'john@example.com', 30);

-- Multiple rows
INSERT INTO users (name, email, age)
VALUES 
    ('Jane Smith', 'jane@example.com', 25),
    ('Bob Johnson', 'bob@example.com', 35),
    ('Alice Williams', 'alice@example.com', 28);

-- Insert from SELECT
INSERT INTO archived_orders (order_id, user_id, total, created_at)
SELECT id, user_id, total, created_at
FROM orders
WHERE created_at < '2023-01-01';

-- UPDATE STATEMENTS
-- ================================================================================

-- Simple update
UPDATE users 
SET age = 31 
WHERE id = 1;

-- Multiple columns
UPDATE products
SET price = 99.99, stock = 50, updated_at = CURRENT_TIMESTAMP
WHERE id = 100;

-- Update with calculation
UPDATE products
SET price = price * 1.1
WHERE category = 'electronics';

-- Update from JOIN
UPDATE orders o
SET status = 'completed'
FROM payments p
WHERE o.id = p.order_id AND p.status = 'confirmed';

-- DELETE STATEMENTS
-- ================================================================================

-- Simple delete
DELETE FROM users WHERE id = 1;

-- Delete with condition
DELETE FROM orders 
WHERE status = 'cancelled' AND created_at < '2023-01-01';

-- Delete all rows
DELETE FROM temp_table;

-- CREATE TABLE
-- ================================================================================

-- Basic table
CREATE TABLE users (
    id INTEGER PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    email VARCHAR(255) UNIQUE NOT NULL,
    age INTEGER,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Table with constraints
CREATE TABLE orders (
    id INTEGER PRIMARY KEY,
    user_id INTEGER NOT NULL,
    total DECIMAL(10, 2) NOT NULL,
    status VARCHAR(50) DEFAULT 'pending',
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (user_id) REFERENCES users(id),
    CHECK (total >= 0)
);

-- Table with composite primary key
CREATE TABLE order_items (
    order_id INTEGER,
    product_id INTEGER,
    quantity INTEGER NOT NULL,
    price DECIMAL(10, 2) NOT NULL,
    PRIMARY KEY (order_id, product_id),
    FOREIGN KEY (order_id) REFERENCES orders(id),
    FOREIGN KEY (product_id) REFERENCES products(id)
);

-- CREATE INDEX
-- ================================================================================

CREATE INDEX idx_users_email ON users(email);
CREATE INDEX idx_orders_user_id ON orders(user_id);
CREATE INDEX idx_products_category_price ON products(category, price);
CREATE UNIQUE INDEX idx_users_username ON users(username);

-- ALTER TABLE
-- ================================================================================

-- Add column
ALTER TABLE users ADD COLUMN phone VARCHAR(20);

-- Drop column
ALTER TABLE users DROP COLUMN age;

-- Add constraint
ALTER TABLE orders ADD CONSTRAINT fk_user 
    FOREIGN KEY (user_id) REFERENCES users(id);

-- Drop constraint
ALTER TABLE orders DROP CONSTRAINT fk_user;

-- DROP STATEMENTS
-- ================================================================================

DROP TABLE temp_data;
DROP TABLE IF EXISTS old_logs;
DROP INDEX idx_old;

-- TRANSACTION CONTROL
-- ================================================================================

BEGIN TRANSACTION;
UPDATE accounts SET balance = balance - 100 WHERE id = 1;
UPDATE accounts SET balance = balance + 100 WHERE id = 2;
COMMIT;

-- Rollback example
BEGIN TRANSACTION;
DELETE FROM important_data;
ROLLBACK;

-- Savepoint
BEGIN TRANSACTION;
UPDATE products SET price = price * 1.1;
SAVEPOINT price_update;
UPDATE products SET stock = 0 WHERE discontinued = true;
ROLLBACK TO SAVEPOINT price_update;
COMMIT;

-- ADVANCED EXAMPLES
-- ================================================================================

-- Complex analytical query
WITH monthly_sales AS (
    SELECT 
        DATE_TRUNC('month', order_date) as month,
        product_id,
        SUM(quantity * price) as revenue
    FROM order_items
    GROUP BY DATE_TRUNC('month', order_date), product_id
),
ranked_products AS (
    SELECT 
        month,
        product_id,
        revenue,
        RANK() OVER (PARTITION BY month ORDER BY revenue DESC) as rank
    FROM monthly_sales
)
SELECT 
    month,
    product_id,
    revenue
FROM ranked_products
WHERE rank <= 5
ORDER BY month, rank;

-- Pivot-like query
SELECT 
    product_id,
    SUM(CASE WHEN EXTRACT(QUARTER FROM order_date) = 1 THEN revenue ELSE 0 END) as Q1,
    SUM(CASE WHEN EXTRACT(QUARTER FROM order_date) = 2 THEN revenue ELSE 0 END) as Q2,
    SUM(CASE WHEN EXTRACT(QUARTER FROM order_date) = 3 THEN revenue ELSE 0 END) as Q3,
    SUM(CASE WHEN EXTRACT(QUARTER FROM order_date) = 4 THEN revenue ELSE 0 END) as Q4
FROM (
    SELECT 
        product_id,
        order_date,
        quantity * price as revenue
    FROM order_items
) subquery
GROUP BY product_id;

-- Running totals
SELECT 
    order_date,
    order_id,
    total,
    SUM(total) OVER (ORDER BY order_date, order_id) as running_total
FROM orders;

-- Moving average
SELECT 
    date,
    revenue,
    AVG(revenue) OVER (
        ORDER BY date 
        ROWS BETWEEN 6 PRECEDING AND CURRENT ROW
    ) as moving_avg_7days
FROM daily_revenue;

-- Year-over-year comparison
SELECT 
    current_year.month,
    current_year.revenue as current_revenue,
    previous_year.revenue as previous_revenue,
    (current_year.revenue - previous_year.revenue) / previous_year.revenue * 100 as growth_percentage
FROM (
    SELECT DATE_TRUNC('month', order_date) as month, SUM(total) as revenue
    FROM orders
    WHERE EXTRACT(YEAR FROM order_date) = 2024
    GROUP BY DATE_TRUNC('month', order_date)
) current_year
LEFT JOIN (
    SELECT DATE_TRUNC('month', order_date) as month, SUM(total) as revenue
    FROM orders
    WHERE EXTRACT(YEAR FROM order_date) = 2023
    GROUP BY DATE_TRUNC('month', order_date)
) previous_year ON current_year.month - INTERVAL '1 year' = previous_year.month;

-- ================================================================================
-- END OF SQL EXAMPLES
-- ================================================================================

