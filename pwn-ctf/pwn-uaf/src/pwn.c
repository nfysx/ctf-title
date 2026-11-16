/* pwn-uaf
 * note = { char data[32]; void (*func)(char *); }
 * free() does not clear the slot, and edit() still writes through the
 * dangling pointer, so edit-after-free rewrites func to win_note().
 */
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <unistd.h>

typedef void (*fn_t)(char *);

struct note {
    char data[32];
    fn_t func;
};

static struct note *notes[8];

static void show_note(char *data) {
    printf("[*] %s\n", data);
}

static void win_note(char *data) {
    (void)data;
    system("cat /app/flag");
}

static int read_int(void) {
    char line[16];
    if (!fgets(line, sizeof line, stdin))
        return -1;
    return atoi(line);
}

int main(void) {
    int i;

    setvbuf(stdout, NULL, _IONBF, 0);
    for (i = 0; i < 8; i++)
        notes[i] = NULL;

    while (1) {
        printf("1. new  2. edit  3. free  4. run  5. exit\n> ");
        switch (read_int()) {
        case 1:
            for (i = 0; i < 8; i++)
                if (!notes[i])
                    break;
            if (i == 8) { puts("full"); break; }
            notes[i] = malloc(sizeof(struct note));
            notes[i]->func = show_note;
            memset(notes[i]->data, 0, sizeof notes[i]->data);
            printf("data: ");
            if (!fgets(notes[i]->data, sizeof notes[i]->data, stdin))
                return 0;
            puts("created");
            break;
        case 2:
            printf("idx: ");
            i = read_int();
            if (i < 0 || i > 7 || !notes[i]) { puts("invalid"); break; }
            printf("data: ");
            read(0, notes[i], sizeof(struct note));
            puts("edited");
            break;
        case 3:
            printf("idx: ");
            i = read_int();
            if (i < 0 || i > 7 || !notes[i]) { puts("invalid"); break; }
            free(notes[i]);
            puts("freed");
            break;
        case 4:
            printf("idx: ");
            i = read_int();
            if (i < 0 || i > 7 || !notes[i]) { puts("invalid"); break; }
            notes[i]->func(notes[i]->data);
            break;
        case 5:
            return 0;
        default:
            break;
        }
    }
}
