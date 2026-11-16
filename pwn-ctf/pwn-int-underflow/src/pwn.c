/* pwn-int-underflow
 * len is unsigned and the bound check casts it to int, so 0xffffffff passes
 * as -1 and read(0, buf, 0xffffffff) smashes the stack.
 *
 * win 带 force_align_arg_pointer：ret 进入时栈对齐与 call 进入不同。
 */
#include <stdio.h>
#include <stdlib.h>
#include <unistd.h>

__attribute__((force_align_arg_pointer))
void win(void) {
    system("cat /app/flag");
}

int main(void) {
    char buf[64];
    unsigned int len = 0;

    setvbuf(stdout, NULL, _IONBF, 0);
    puts("send length (4 bytes little endian):");
    if (read(0, &len, sizeof len) != (ssize_t)sizeof len)
        return 1;

    if ((int)len > 64) {
        puts("too long");
        return 1;
    }

    puts("send data:");
    read(0, buf, len);
    puts("done");
    return 0;
}
