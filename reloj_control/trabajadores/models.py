from django.db import models
from datetime import time, timedelta
import random

class Trabajador(models.Model):
    ID = models.IntegerField(primary_key=True)
    fecha = models.DateField()
    tiempo_entrada = models.TimeField()
    tiempo_salida = models.TimeField()
    tiempo_trabajado = models.TimeField()

    @property
    def tiempo_trabajado(self):
        minutos_entrada = self.tiempo_entrada.hour*60 + self.tiempo_entrada.minute
        minutos_salida = self.tiempo_salida.hour*60 + self.tiempo_salida.minute

        diferencia_minutos = minutos_salida-minutos_entrada
        horas = diferencia_minutos//60
        minutos = diferencia_minutos%60

        tiempo_trabajado = time(hour=horas,minute=minutos)
        return tiempo_trabajado




    def __str__(self):
        return f"ID: {self.ID}, Fecha: {self.fecha}, Horas Trabajadas: {self.tiempo_trabajado}"

    
    