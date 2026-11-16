#include <stdio.h>
#include <string.h>

#define LEN 41
#define BLOCKS 11

/* y = M * x (mod 256), x = 4 input bytes */
static const unsigned char M[4][4] = {
    {0x98, 0x92, 0xf8, 0x1d},
    {0xa6, 0xc5, 0x93, 0x79},
    {0x50, 0x9d, 0xc4, 0x85},
    {0x5f, 0xf2, 0xcd, 0x9a},
};

unsigned char T[44] = {
    0xcb, 0xa2, 0xe3, 0x95, 0x61, 0x7b, 0xad, 0x57, 0xc0, 0xfa, 0xc5, 0x6c,
    0x0d, 0xa6, 0x58, 0x4b, 0xf3, 0xfb, 0x78, 0x1a, 0x3d, 0xef, 0x9a, 0x0a,
    0xd5, 0x6f, 0x00, 0xf5, 0x8d, 0xc2, 0x73, 0x72, 0xee, 0xd6, 0x99, 0xfe,
    0x3d, 0x06, 0xda, 0x29, 0x38, 0x0e, 0x10, 0x63,
};

static int check(const char *in)
{
    unsigned char x[4];
    int i, r, n = (int)strlen(in);

    if (n != LEN)
        return 0;
    for (i = 0; i < BLOCKS; i++) {
        memcpy(x, in + i * 4, 4);
        for (r = 0; r < 4; r++) {
            unsigned int acc = 0;
            int c;
            for (c = 0; c < 4; c++)
                acc += (unsigned int)M[r][c] * x[c];
            if ((unsigned char)acc != T[i * 4 + r])
                return 0;
        }
    }
    return 1;
}

int main(void)
{
    char buf[128];

    printf("input your flag: ");
    fflush(stdout);
    if (scanf("%127s", buf) != 1)
        return 1;
    if (check(buf)) {
        puts("Good!");
        return 0;
    }
    puts("Wrong!");
    return 1;
}
