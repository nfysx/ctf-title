# pdo_mysql 走内置 mysqlnd，不需要系统库，可离线编译
FROM php:7.4-apache

RUN docker-php-ext-install pdo_mysql

COPY src/ /var/www/html/
EXPOSE 80
