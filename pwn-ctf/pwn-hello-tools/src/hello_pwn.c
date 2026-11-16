/* pwn-hello-tools —— 引入题：file / checksec / strings / objdump 四件套
 *
 * 编译（容器内，gcc 11.4 / ubuntu 22.04）：
 *   gcc -o files/hello_pwn src/hello_pwn.c -fno-stack-protector -no-pie
 *
 * flag 逐字节异或 0x37 后放在 .rodata，strings 不会直接吐出明文；
 * key 是明文常量，strings 能看见。两件事分别对应两个工具。
 */
#include <stdio.h>
#include <string.h>

static const unsigned char enc_flag[] = {
    0x51, 0x5b, 0x56, 0x50, 0x4c, 0x51, 0x5e, 0x5b, 0x52, 0x68, 0x54, 0x5f,
    0x52, 0x54, 0x5c, 0x44, 0x52, 0x54, 0x68, 0x44, 0x43, 0x45, 0x5e, 0x59,
    0x50, 0x44, 0x68, 0x58, 0x55, 0x5d, 0x53, 0x42, 0x5a, 0x47, 0x4a,
};

/* 玩家要在 strings 的输出里找到这一串 */
static const char secret_key[] = "pwn_rookie_2026";

int main(void) {
    char buf[64];
    unsigned char out[sizeof enc_flag + 1];

    setvbuf(stdout, NULL, _IONBF, 0);

    printf("Enter key: ");
    if (!fgets(buf, sizeof buf, stdin))
        return 1;
    buf[strcspn(buf, "\n")] = '\0';

    if (strcmp(buf, secret_key) != 0) {
        puts("Access denied.");
        return 1;
    }

    for (size_t i = 0; i < sizeof enc_flag; i++)
        out[i] = (unsigned char)(enc_flag[i] ^ 0x37);
    out[sizeof enc_flag] = '\0';

    puts("Access granted.");
    puts((char *)out);
    return 0;
}
