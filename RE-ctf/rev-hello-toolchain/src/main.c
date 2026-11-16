#include <stdio.h>
#include <string.h>

/* KEY1: a plain constant, visible in the output of "strings". */
static const char KEY1[] = "toolchain_in_";

/*
 * KEY2: deliberately NOT a string constant.
 * The bytes are written one by one into a volatile buffer, so the compiler
 * cannot fold them into a blob in .rdata: it has to emit a run of
 * "movb $0x??" immediates, which shows up in "objdump -d" only.
 */
static void build_key2(volatile char *dst)
{
    dst[0]  = 't';
    dst[1]  = 'h';
    dst[2]  = 'r';
    dst[3]  = 'e';
    dst[4]  = 'e';
    dst[5]  = '_';
    dst[6]  = 'c';
    dst[7]  = 'o';
    dst[8]  = 'm';
    dst[9]  = 'm';
    dst[10] = 'a';
    dst[11] = 'n';
    dst[12] = 'd';
    dst[13] = 's';
    dst[14] = 0;
}

int main(void)
{
    char buf[256];
    char key2[32];
    char expect[128];

    printf("==============================================\n");
    printf(" RE intro - lesson 1: three commands\n");
    printf("==============================================\n");
    printf("Two keys are hidden in this program. Concatenate them.\n");
    printf("  KEY1: find it with  strings hello_re.exe\n");
    printf("        the only lowercase, underscore-joined phrase,\n");
    printf("        starting with a letter and ending with '_'\n");
    printf("  KEY2: find it with  objdump -d hello_re.exe\n");
    printf("        a run of movb $0x?? immediates: its ASCII bytes,\n");
    printf("        read in order, 0x00 marks the end\n");
    printf("  submit as: flag{KEY1KEY2}\n");
    printf("----------------------------------------------\n");

    build_key2(key2);
    snprintf(expect, sizeof expect, "flag{%s%s}", KEY1, (const char *)key2);

    printf("input the flag: ");
    fflush(stdout);
    if (scanf("%255s", buf) != 1)
        return 1;

    if (strcmp(buf, expect) == 0) {
        puts("Good!");
        return 0;
    }
    puts("Wrong!");
    return 1;
}
