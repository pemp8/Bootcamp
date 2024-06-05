from django.db import models
from datetime import time

class Trabajador(models.Model):
    ID = models.IntegerField(primary_key=True)

    def __str__(self):
        return f"ID: {self.ID}"

class RegistroTiempo(models.Model):
    trabajador = models.ForeignKey(Trabajador, on_delete=models.CASCADE)
    fecha = models.DateField()
    tiempo_entrada = models.TimeField()
    tiempo_salida = models.TimeField()
    tiempo_trabajado = models.TimeField(null=True, blank=True)  # Este campo puede ser calculado o llenado opcionalmente

    def calcular_tiempo_trabajado(self):
        minutos_entrada = self.tiempo_entrada.hour * 60 + self.tiempo_entrada.minute
        minutos_salida = self.tiempo_salida.hour * 60 + self.tiempo_salida.minute

        diferencia_minutos = minutos_salida - minutos_entrada
        horas = diferencia_minutos // 60
        minutos = diferencia_minutos % 60

        self.tiempo_trabajado = time(hour=horas, minute=minutos)

    def save(self, *args, **kwargs):
        # Calcular el tiempo trabajado antes de guardar
        self.calcular_tiempo_trabajado()
        # Desactivar las señales para evitar la recursión infinita
        super().save(*args, **kwargs)

    def __str__(self):
        return f"Trabajador: {self.trabajador.ID}, Fecha: {self.fecha}, Horas Trabajadas: {self.tiempo_trabajado}"
