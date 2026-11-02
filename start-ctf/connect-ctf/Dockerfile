FROM php:7.4-apache

# 平台（ret2shell 跑在 k8s 里）会以 restricted 方式起题目容器，
# 容器可能被丢掉 CAP_NET_BIND_SERVICE，此时 root 也绑不上 80。
# 所以统一监听 8080（>=1024 之外的高位端口总会放行）。
RUN sed -ri 's/^Listen 80$/Listen 8080/' /etc/apache2/ports.conf \
 && sed -ri 's/<VirtualHost \*:80>/<VirtualHost *:8080>/' /etc/apache2/sites-available/000-default.conf

COPY src/ /var/www/html/
EXPOSE 8080
