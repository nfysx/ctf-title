/* pwn-fmt-write
 * printf(buf) with attacker-controlled buf: arbitrary write via %n.
 * Overwrite the GOT entry of exit with backdoor().
 */
#include <stdio.h>
#include <stdlib.h>

void backdoor(void) {
    system("cat /app/flag");
}

int main(void) {
    char buf[128];

    setvbuf(stdout, NULL, _IONBF, 0);
    printf("name: ");
    fgets(buf, sizeof buf, stdin);
    printf(buf);
    printf("\n");
    exit(0);
}
