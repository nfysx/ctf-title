/* pwn-ret2shellcode
 * NX disabled (-z execstack)。程序把栈缓冲区地址打印出来，read 之后
 * 返回地址被覆盖成该地址，直接跳到栈上执行 shellcode。
 *
 * 缓冲区给到 128 字节：shellcode 会往当前栈顶 push 字符串，
 * 缓冲区太小的话这些 push 会盖掉尚未执行的指令。
 */
#include <stdio.h>
#include <unistd.h>

int main(void) {
    char buf[128];

    setvbuf(stdout, NULL, _IONBF, 0);
    printf("stack: %p\n", (void *)buf);
    read(0, buf, 0x200);
    return 0;
}
