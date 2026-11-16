#include <stdio.h>
#include <string.h>
#include <unistd.h>
#include <sys/ptrace.h>

#define LEN 36

unsigned char K[8] = {
    0x2e, 0x21, 0x52, 0x3f, 0xf1, 0x06, 0x94, 0x01,
};

unsigned char T[36] = {
    0x84, 0xe7, 0x59, 0xbe, 0xf4, 0xd5, 0x21, 0xdc, 0x0c, 0x92, 0x21, 0x76,
    0x1d, 0x2e, 0x49, 0x02, 0x94, 0xc7, 0xb9, 0x0e, 0xa4, 0x24, 0xb0, 0x4b,
    0x7c, 0xc2, 0xf0, 0xa6, 0x1d, 0xcd, 0xf6, 0x64, 0x54, 0xc8, 0x29, 0xbd,
};

/* Linux 下的自检：已被调试器跟踪时 ptrace(PTRACE_TRACEME) 返回 -1 */
static int being_traced(void)
{
    return ptrace(PTRACE_TRACEME, 0, NULL, NULL) == -1;
}

static int real_check(const char *in)
{
    int i, n = (int)strlen(in);
    unsigned char v;

    if (n != LEN)
        return 0;
    for (i = 0; i < LEN; i++) {
        v = (unsigned char)(in[i] ^ K[i & 7]);
        v = (unsigned char)((v << 4) | (v >> 4));
        v = (unsigned char)(v + i * 0x13);
        if (v != T[i])
            return 0;
    }
    return 1;
}

int main(void)
{
    char buf[256];

    printf("input your flag: ");
    fflush(stdout);
    if (scanf("%255s", buf) != 1)
        return 1;

    if (being_traced())
        return 1;

    if (real_check(buf)) {
        puts("Good!");
        return 0;
    }
    puts("Wrong!");
    return 1;
}