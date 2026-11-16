# pdo_mysql 走内置 mysqlnd，不需要系统库，可离线编译
FROM php:7.4-apache

# 平台（ret2shell 跑在 k8s 里）以 restricted 方式起容器并 drop CAP_NET_BIND_SERVICE，
# root 也绑不上 80（AH00072: make_sock: Permission denied），统一改成 8080。
RUN sed -ri 's/^Listen 80$/Listen 8080/' /etc/apache2/ports.conf \
 && sed -ri 's/<VirtualHost \*:80>/<VirtualHost *:8080>/' /etc/apache2/sites-available/000-default.conf

RUN docker-php-ext-install pdo_mysql

COPY src/ /var/www/html/
EXPOSE 8080
