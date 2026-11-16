/* pwn-ret2libc
 * 二进制里没有任何打印 flag 的函数。先用 puts@plt 泄露 puts@got，
 * 再回到 vuln() 打第二轮，最后 ret2libc 调 system("/bin/sh")。
 *
 * 二进制的 .text 里显式留了 pop rdi; ret，stage1 需要的传参 gadget
 * 不必去 libc 里找（此时还不知道 libc 基址）。
 */
#include <stdio.h>
#include <unistd.h>

__asm__(
    ".global pop_rdi_ret\n"
    ".type pop_rdi_ret, @function\n"
    "pop_rdi_ret:\n"
    "  pop %rdi\n"
    "  ret\n"
);

void vuln(void) {
    char buf[64];

    read(0, buf, 0x200);
}

int main(void) {
    setvbuf(stdout, NULL, _IONBF, 0);
    puts("input:");
    vuln();
    puts("done");
    return 0;
}
