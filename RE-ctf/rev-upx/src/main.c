#include <stdio.h>
#include <string.h>

static const char KEYSTR[] = "flag{upx_packed_not_protected}";

int main(void)
{
    char buf[128];

    printf("input your flag: ");
    fflush(stdout);
    if (scanf("%127s", buf) != 1)
        return 1;
    if (strcmp(buf, KEYSTR) == 0) {
        puts("Good!");
        return 0;
    }
    puts("Wrong!");
    return 1;
}
