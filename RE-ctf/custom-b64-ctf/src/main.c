/*
 * custom-b64 —— 换过表的 Base64 校验
 *
 * 读一行输入，按标准 Base64 的 3→4 分组编码，但 64 个输出字符用的是自定义表，
 * 然后和目标密文逐字符比较。
 *
 * 编译： gcc -O2 -s -o challenge main.c
 */
#include <stdio.h>
#include <string.h>

static const char TBL[65] =
    "2KuEphj84USZF67iloxzfYd+MrDgRG9yLwBnHAXcJq3eCN/s1bOQ5TvPa0tVkWmI";

static const char TARGET[] =
    "rXbwrPNnG+65gvTyMnM5+vhCRjwwMXY5yl==";

static void encode(const unsigned char *in, int len, char *out)
{
    int i, o = 0;
    for (i = 0; i + 2 < len; i += 3) {
        out[o++] = TBL[(in[i] >> 2) & 0x3f];
        out[o++] = TBL[((in[i] & 0x03) << 4) | ((in[i + 1] >> 4) & 0x0f)];
        out[o++] = TBL[((in[i + 1] & 0x0f) << 2) | ((in[i + 2] >> 6) & 0x03)];
        out[o++] = TBL[in[i + 2] & 0x3f];
    }
    if (len - i == 1) {
        out[o++] = TBL[(in[i] >> 2) & 0x3f];
        out[o++] = TBL[(in[i] & 0x03) << 4];
        out[o++] = '=';
        out[o++] = '=';
    } else if (len - i == 2) {
        out[o++] = TBL[(in[i] >> 2) & 0x3f];
        out[o++] = TBL[((in[i] & 0x03) << 4) | ((in[i + 1] >> 4) & 0x0f)];
        out[o++] = TBL[(in[i + 1] & 0x0f) << 2];
        out[o++] = '=';
    }
    out[o] = 0;
}

int main(void)
{
    char buf[256];
    char enc[512];

    printf("input your flag: ");
    fflush(stdout);
    if (scanf("%255s", buf) != 1)
        return 1;

    encode((const unsigned char *)buf, (int)strlen(buf), enc);

    if (strcmp(enc, TARGET) == 0) {
        puts("Good!");
        return 0;
    }
    puts("Wrong!");
    return 1;
}
