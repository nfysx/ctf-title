/* pwn-ret2text
 * gets() into buf[64] overflows the saved return address of main.
 * Overwrite it with the address of backdoor().
 *
 * backdoor 带 force_align_arg_pointer：ret 进入函数时 rsp 的 16 字节对齐
 * 与 call 进入不同，缺这条属性时 glibc 的 system() 会在 movaps 上崩溃。
 */
#include <stdio.h>
#include <stdlib.h>

extern char *gets(char *s);

__attribute__((force_align_arg_pointer))
void backdoor(void) {
    system("cat /app/flag");
}

int main(void) {
    char buf[64];

    setvbuf(stdout, NULL, _IONBF, 0);
    puts("input:");
    gets(buf);
    puts("bye");
    return 0;
}
