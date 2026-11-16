#include <stdio.h>
#include <string.h>

#define LEN 26
#define KEYLEN 8

unsigned char KEY[8] = {
    0x52, 0xc4, 0x1f, 0x9b, 0x6e, 0x33, 0xa8, 0x07,
};

unsigned char ENC[26] = {
    0x78, 0xf7, 0x36, 0xd9, 0x08, 0xc2, 0xa1, 0x43, 0xe4, 0x33, 0x8a, 0x72,
    0x25, 0x62, 0x84, 0xb6, 0x3e, 0xcc, 0xa2, 0xba, 0x01, 0xb8, 0x1c, 0x37,
    0xeb, 0x58,
};

static void rc4(const unsigned char *key, int klen, const unsigned char *in,
                unsigned char *out, int n)
{
    unsigned char s[256], tmp;
    int i, j = 0, a = 0, b = 0;

    for (i = 0; i < 256; i++)
        s[i] = (unsigned char)i;
    for (i = 0; i < 256; i++) {
        j = (j + s[i] + key[i % klen]) & 0xff;
        tmp = s[i]; s[i] = s[j]; s[j] = tmp;
    }
    for (i = 0; i < n; i++) {
        a = (a + 1) & 0xff;
        b = (b + s[a]) & 0xff;
        tmp = s[a]; s[a] = s[b]; s[b] = tmp;
        out[i] = (unsigned char)(in[i] ^ s[(s[a] + s[b]) & 0xff]);
    }
}

int main(void)
{
    char buf[256];
    unsigned char out[LEN];

    printf("input your flag: ");
    fflush(stdout);
    if (scanf("%255s", buf) != 1)
        return 1;
    if ((int)strlen(buf) != LEN) {
        puts("Wrong!");
        return 1;
    }
    rc4(KEY, KEYLEN, (const unsigned char *)buf, out, LEN);
    if (memcmp(out, ENC, LEN) == 0) {
        puts("Good!");
        return 0;
    }
    puts("Wrong!");
    return 1;
}
