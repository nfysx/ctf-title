FROM php:7.4-apache

# 演示「报错页面泄露」：把错误显示打开
RUN printf 'display_errors=On\ndisplay_startup_errors=On\nerror_reporting=E_ALL\ndate.timezone=Asia/Shanghai\n' \
      > /usr/local/etc/php/conf.d/99-demo.ini

# 演示「响应头泄露」：加一个自称调试用的响应头
RUN a2enmod headers \
 && printf 'Header always add X-Debug-Path /var/www/html\n' > /etc/apache2/conf-enabled/zz-demo.conf \
 && printf '<Directory /var/www/html/backup>\n  Options +Indexes\n  Require all granted\n</Directory>\n' \
      >> /etc/apache2/conf-enabled/zz-demo.conf

COPY src/ /var/www/html/

# 真实的 .git 目录（放在 dotgit/ 里，构建时映射成网站根目录下的 .git）
COPY dotgit/ /var/www/html/.git/

EXPOSE 80
