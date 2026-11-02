-- 初始化脚本：MariaDB 首次启动时自动执行
CREATE TABLE IF NOT EXISTS users (
  id       INT AUTO_INCREMENT PRIMARY KEY,
  username VARCHAR(64)  NOT NULL,
  password VARCHAR(128) NOT NULL,
  role     VARCHAR(16)  NOT NULL
);

INSERT INTO users (username, password, role) VALUES
  ('user', 'user', 'user'),
  ('guest', 'guest', 'guest'),
  ('admin', '18fcbe4d1b4d4ca61233f0e49293f8df', 'admin');
