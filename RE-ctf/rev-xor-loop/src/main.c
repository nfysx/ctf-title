#include <stdio.h>
#include <string.h>

#define LEN 28

static const unsigned char KEY[6] = {'K', '3', 'y', '!', '2', '6'};

unsigned char ENC[28] = {
    0x2d, 0x5e, 0x1a, 0x45, 0x4d, 0x4b, 0x22, 0x46, 0x2e, 0x5f, 0x51, 0x49,
    0x2f, 0x61, 0x16, 0x71, 0x50, 0x48, 0x35, 0x4c, 0x04, 0x5a, 0x43, 0x7e,
    0x38, 0x4f, 0x1a, 0x47,
};

int main(void)
{
    char buf[256];
    unsigned char out[LEN];
    int i, n;

    printf("input your flag: ");
    fflush(stdout);
    if (scanf("%255s", buf) != 1)
        return 1;
    n = (int)strlen(buf);
    if (n != LEN) {
        puts("Wrong!");
        return 1;
    }
    for (i = 0; i < LEN; i++) {
        out[i] = (unsigned char)(buf[i] ^ KEY[i % 6] ^ (i & 0xff));
    }
    if (memcmp(out, ENC, LEN) == 0) {
        puts("Good!");
        return 0;
    }
    puts("Wrong!");
    return 1;
}
