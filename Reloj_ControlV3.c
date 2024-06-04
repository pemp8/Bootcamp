#include <stdio.h>
#include <stdlib.h>
#include <time.h>

/*Numero de trabajadores*/
#define numTrabajadores 10 

/*Estructura para cada trabajador*/
typedef struct {
    int ID;
    struct tm fecha;
    struct tm tiempo_entrada;
    struct tm tiempo_salida;
    struct tm tiempo_trabajado;
} trabajador;

/*Funcion para obtener un numero aleatorio en el rango "min : max"*/
int randomNum(int min, int max){
    return (rand() % (max - min + 1)) + min;
}

/*Funcion para imprimir datos del trabajador en formato JSON*/
void print_trabajador_json(trabajador *t) {
    char fecha_str[11], tiempo_entrada_str[6], tiempo_salida_str[6], tiempo_trabajado_str[6];
    strftime(fecha_str, sizeof(fecha_str), "%Y-%m-%d", &t->fecha);
    strftime(tiempo_entrada_str, sizeof(tiempo_entrada_str), "%H:%M", &t->tiempo_entrada);
    strftime(tiempo_salida_str, sizeof(tiempo_salida_str), "%H:%M", &t->tiempo_salida);
    strftime(tiempo_trabajado_str, sizeof(tiempo_trabajado_str), "%H:%M", &t->tiempo_trabajado);

    printf("{\"ID\": %d, \"fecha\": \"%s\", \"tiempo_entrada\": \"%s\", \"tiempo_salida\": \"%s\", \"tiempo_trabajado\": \"%s\"}", 
           t->ID, fecha_str, tiempo_entrada_str, tiempo_salida_str, tiempo_trabajado_str);
}

int main() {
    srand(time(NULL));
    trabajador trabajadores[numTrabajadores];

    /*Fecha y hora actuales*/
    time_t actual = time(NULL);
    struct tm *tm_actual = localtime(&actual);

    /*Se crean los datos de cada trabajador (ID, tiempo entrada, tiempo salida y horas trabajadas)*/
    printf("[");
    for(int i = 0; i < numTrabajadores; i++) {
        if (i > 0) {
            printf(",");
        }

        /*Se crea el ID de cada trabajador*/
        trabajadores[i].ID = rand() % 10000;

        /*Se crea una variable "ausente" como un numero aleatorio entre 1 y 10*/
        int ausente = randomNum(1, 10);

        /*Si la variable "ausente" es distinto de 5, el trabajador si se presento a trabajar*/
        if (ausente != 5) {
            /*Se deja la fecha como la fecha actual del dia de hoy*/
            trabajadores[i].fecha = *tm_actual;

            /*Se crea la hora de entrada en el rango 8:00 y 11:00 y la hora de salida en el rango 16:00 y 19:00*/
            trabajadores[i].tiempo_entrada = (struct tm){.tm_hour = rand() % 4 + 8, .tm_min = rand() % 60};
            trabajadores[i].tiempo_salida = (struct tm){.tm_hour = rand() % 4 + 16, .tm_min = rand() % 60};

            /*Se separa el tiempo de entrada y salida en horas y minutos*/
            int hora_entrada = trabajadores[i].tiempo_entrada.tm_hour;
            int minuto_entrada = trabajadores[i].tiempo_entrada.tm_min;
            int hora_salida = trabajadores[i].tiempo_salida.tm_hour;
            int minuto_salida = trabajadores[i].tiempo_salida.tm_min;

            /*Se convierte todo a minutos*/
            int tiempo_entrada_en_minutos = hora_entrada * 60 + minuto_entrada;
            int tiempo_salida_en_minutos = hora_salida * 60 + minuto_salida;

            /*Se calcula el tiempo trabajado en minutos*/
            int tiempo_trabajado_en_minutos = tiempo_salida_en_minutos - tiempo_entrada_en_minutos;
            /*Se separa en horas y minutos*/
            int horas_trabajadas = (int)tiempo_trabajado_en_minutos/60;
            int minutos_trabajados = (tiempo_trabajado_en_minutos - horas_trabajadas*60);
            /*Se guardan las horas y minutos en "tiempo_trabajado"*/
            trabajadores[i].tiempo_trabajado = (struct tm){.tm_hour = horas_trabajadas, .tm_min = minutos_trabajados};

            /*Imprimir datos del trabajador en formato JSON*/
            print_trabajador_json(&trabajadores[i]);
        } else {
            /*Si la variable "ausente" es 5, se printea con los datos en 0*/
            trabajadores[i].fecha = *tm_actual;
            trabajadores[i].tiempo_entrada = (struct tm){.tm_hour = 0, .tm_min = 0};
            trabajadores[i].tiempo_salida = (struct tm){.tm_hour = 0, .tm_min = 0};
            trabajadores[i].tiempo_trabajado = (struct tm){.tm_hour = 0, .tm_min = 0};
            print_trabajador_json(&trabajadores[i]);
            
        }
    }
    printf("]\n");

    return 0;
}
