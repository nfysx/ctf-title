/* pwn-one-byte
 * read() of sizeof(name)+1 writes exactly one byte past name[] and lands on
 * the low byte of the adjacent global int auth.
 */
#include <stdio.h>
#include <stdlib.h>
#include <unistd.h>

char name[16];
int auth = 0;

int main(void) {
    setvbuf(stdout, NULL, _IONBF, 0);
    puts("name:");
    read(0, name, sizeof(name) + 1);

    if (auth) {
        puts("welcome, admin");
        system("cat /app/flag");
    } else {
        puts("auth failed");
    }
    return 0;
}
