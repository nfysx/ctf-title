#include <stdio.h>
#include <string.h>

#define LEN 29
#define MASK 0x5a

unsigned char ENC[29] = {
    0x3c, 0x36, 0x3b, 0x3d, 0x21, 0x3c, 0x36, 0x33, 0x2a, 0x05, 0x2e, 0x32,
    0x3f, 0x05, 0x38, 0x28, 0x3b, 0x34, 0x39, 0x32, 0x05, 0x3b, 0x34, 0x3e,
    0x05, 0x2d, 0x33, 0x34, 0x27,
};

int main(void)
{
    char buf[128], real[LEN + 1];
    int i;

    printf("input your flag: ");
    fflush(stdout);
    if (scanf("%127s", buf) != 1)
        return 1;

    for (i = 0; i < LEN; i++)
        real[i] = (char)(ENC[i] ^ MASK);
    real[LEN] = 0;

    if (strcmp(buf, real) == 0) {
        puts("Wrong!");
        return 1;
    }
    puts("Good!");
    return 0;
}
