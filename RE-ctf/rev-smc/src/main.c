#include <stdio.h>
#include <string.h>
#include <stdint.h>
#include <unistd.h>
#include <sys/mman.h>

#define LEN 36

/* 解密密钥留在 .text 里（明文） */
static const unsigned char SMK[8] = {0xee, 0x39, 0xea, 0x31, 0x6a, 0x36, 0xcd, 0xbb};

/* 目标串 = flag ^ TK，放在 .rodata（明文） */
static const unsigned char ENC[36] = {
    0xfb, 0x87, 0x21, 0x16, 0x39, 0xa8, 0xb0, 0xee, 0xfb, 0xb4, 0x2d, 0x1e,
    0x26, 0xb2, 0xb3, 0xfb, 0xf4, 0x85, 0x27, 0x2e, 0x21, 0xb4, 0xb1, 0xe7,
    0xc2, 0x8a, 0x34, 0x2e, 0x30, 0xae, 0xbb, 0xf6, 0xf4, 0x86, 0x25, 0x0c,
};

/* 异或密钥：整个 smcd 节在磁盘上是密文 */
__attribute__((section("smcd"), used))
static const unsigned char TK[8] = {0x9d, 0xeb, 0x40, 0x71, 0x42, 0xdb, 0xd5, 0x82};

/* 校验函数所在的 smcx 节在磁盘上也是密文 */
__attribute__((section("smcx"), used, noinline))
static int smc_check(const char *in)
{
    char real[LEN + 1];
    int i;

    for (i = 0; i < LEN; i++)
        real[i] = (char)(ENC[i] ^ TK[i & 7]);
    real[LEN] = 0;
    return strcmp(in, real) == 0;
}

/* ELF 下由链接器为每个自定义节生成的起止符号 */
extern unsigned char __start_smcx[], __stop_smcx[];
extern unsigned char __start_smcd[], __stop_smcd[];

static void unpatch(unsigned char *start, unsigned char *stop)
{
    size_t len = (size_t)(stop - start);
    size_t i;
    long pg = sysconf(_SC_PAGESIZE);
    uintptr_t lo = (uintptr_t)start & ~(uintptr_t)(pg - 1);
    uintptr_t hi = ((uintptr_t)stop + (uintptr_t)pg - 1) & ~(uintptr_t)(pg - 1);

    if (len == 0)
        return;
    if (mprotect((void *)lo, (size_t)(hi - lo), PROT_READ | PROT_WRITE | PROT_EXEC) != 0)
        return;
    for (i = 0; i < len; i++)
        start[i] ^= SMK[i & 7];
    __builtin___clear_cache((char *)start, (char *)stop);
}

int main(void)
{
    char buf[256];

    printf("input your flag: ");
    fflush(stdout);
    if (scanf("%255s", buf) != 1)
        return 1;

    unpatch(__start_smcx, __stop_smcx);
    unpatch(__start_smcd, __stop_smcd);

    if (smc_check(buf)) {
        puts("Good!");
        return 0;
    }
    puts("Wrong!");
    return 1;
}