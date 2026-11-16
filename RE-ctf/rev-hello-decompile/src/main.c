#include <stdio.h>
#include <string.h>

/*
 * The flag is split into four short constants.
 * The declaration order below is NOT the order they are joined in:
 * "strings" lists them by file offset, which gives the wrong order.
 * The real order lives in main().
 */
static const char P1[] = "_the";
static const char P2[] = "_logic";
static const char P3[] = "decompiler";
static const char P4[] = "_reads";

/*
 * Concatenation goes through a noinline helper, so every join stays a real
 * call: the decompiler shows four calls in source order, and -O2 cannot fold
 * the whole thing into a single constant string.
 */
__attribute__((noinline))
static void append(char *dst, const char *src)
{
    strcat(dst, src);
}

int main(void)
{
    char buf[128];
    char expect[128];

    printf("==============================================\n");
    printf(" RE intro - lesson 2: read the decompiler\n");
    printf("==============================================\n");
    printf("The flag is built from 4 short constants.\n");
    printf("strings shows the pieces, but not the order.\n");
    printf("The order is written in main().\n");
    printf("----------------------------------------------\n");

    strcpy(expect, "flag{");
    append(expect, P3);
    append(expect, P4);
    append(expect, P1);
    append(expect, P2);
    append(expect, "}");

    printf("input the flag: ");
    fflush(stdout);
    if (scanf("%127s", buf) != 1)
        return 1;

    if (strcmp(buf, expect) == 0) {
        puts("Good!");
        return 0;
    }
    puts("Wrong!");
    return 1;
}
