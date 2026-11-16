/* pwn-hello-gdb —— 引入题：gdb 断点 + call win()
 *
 * 编译（容器内，gcc 11.4 / ubuntu 22.04）：
 *   gcc -o files/hello_gdb src/hello_gdb.c -fno-stack-protector -no-pie
 *
 * win() 打印 flag，但 main 里没有任何调用点。
 * 除了 gdb 的 call win()，main 的 read 还留了一个溢出，可以 ret2win。
 */
#include <stdio.h>
#include <unistd.h>

static const unsigned char enc_flag[] = {
    0x3c, 0x36, 0x3b, 0x3d, 0x21, 0x3d, 0x3e, 0x38, 0x05, 0x33, 0x29, 0x05,
    0x23, 0x35, 0x2f, 0x28, 0x05, 0x29, 0x2e, 0x3f, 0x3f, 0x28, 0x33, 0x34,
    0x3d, 0x05, 0x2d, 0x32, 0x3f, 0x3f, 0x36, 0x27,
};

/* ret 跳进 win 时栈比 call 少压一层，这里自己把 rsp 对齐；
   结尾 fflush 是为了在 stdout 被重定向成管道时也能立刻看到输出。 */
__attribute__((force_align_arg_pointer))
void win(void) {
    unsigned char out[sizeof enc_flag + 1];

    for (size_t i = 0; i < sizeof enc_flag; i++)
        out[i] = (unsigned char)(enc_flag[i] ^ 0x5a);
    out[sizeof enc_flag] = '\0';

    puts("You called a function that main never calls.");
    puts((char *)out);
    fflush(stdout);
}

int main(void) {
    char buf[64];

    setvbuf(stdout, NULL, _IONBF, 0);
    puts("main() never calls win(). Prove it is still reachable.");
    read(0, buf, 0x100);
    puts("ok");
    return 0;
}
