/* pwn-canary-leak
 * Stack protector is on.  The first read() is size limited so the canary
 * survives, then printf(buf) leaks it; the second read() overflows.
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

void vuln(void) {
    char buf[64];

    read(0, buf, 63);
    printf("echo: ");
    printf(buf);
    printf("\n");
    read(0, buf, 0x200);
}

int main(void) {
    setvbuf(stdout, NULL, _IONBF, 0);
    vuln();
    return 0;
}
