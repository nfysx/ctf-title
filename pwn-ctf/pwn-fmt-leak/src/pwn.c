/* pwn-fmt-leak
 * printf(buf) with attacker-controlled buf.  The flag is a stack local of
 * main and a stack slot also keeps &flag, so both %N$p (hex words) and
 * %N$s (dereference of the saved pointer) leak it.
 */
#include <stdio.h>
#include <stdlib.h>

static void load_flag(char *dst, size_t n) {
    FILE *f = fopen("/app/flag", "r");
    if (!f) {
        dst[0] = '\0';
        return;
    }
    if (!fgets(dst, (int)n, f))
        dst[0] = '\0';
    fclose(f);
}

int main(void) {
    char flag[64];
    char buf[64];
    char *leak = flag;   /* keeps &flag in a stack slot */

    setvbuf(stdout, NULL, _IONBF, 0);
    load_flag(flag, sizeof flag);

    printf("Say something: ");
    fgets(buf, sizeof buf, stdin);
    printf(buf);
    printf("\n");

    (void)leak;
    return 0;
}
