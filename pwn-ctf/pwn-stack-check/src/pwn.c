/* pwn-stack-check
 * gets() into buf[32] overflows into the adjacent check[8] in main's frame.
 * Set check to "YourFlag" and the program prints the flag.
 */
#include <stdio.h>
#include <string.h>

extern char *gets(char *s);

static void print_flag(void) {
    char line[256];
    FILE *f = fopen("/app/flag", "r");
    if (!f) {
        puts("/app/flag not found");
        return;
    }
    while (fgets(line, sizeof line, f))
        fputs(line, stdout);
    fclose(f);
}

int main(void) {
    char check[8] = "Nope";
    char buf[32];

    setvbuf(stdout, NULL, _IONBF, 0);
    puts("Tell me the password:");
    gets(buf);

    if (memcmp(check, "YourFlag", 8) == 0) {
        puts("Correct!");
        print_flag();
    } else {
        puts("Wrong.");
    }
    return 0;
}
