FROM php:7.4-apache

# 平台（ret2shell 跑在 k8s 里）以 restricted 方式起容器，会 drop CAP_NET_BIND_SERVICE，
# 此时 root 也绑不上 80。所以统一监听 8080（>=1024 的高位端口总会放行）。
RUN sed -ri 's/^Listen 80$/Listen 8080/' /etc/apache2/ports.conf \
 && sed -ri 's/<VirtualHost \*:80>/<VirtualHost *:8080>/' /etc/apache2/sites-available/000-default.conf \
 && echo "expose_php=Off" > /usr/local/etc/php/conf.d/ctf.ini \
 && echo "display_errors=Off" >> /usr/local/etc/php/conf.d/ctf.ini \
 && echo "log_errors=On" >> /usr/local/etc/php/conf.d/ctf.ini

COPY src/ /var/www/html/
EXPOSE 8080
