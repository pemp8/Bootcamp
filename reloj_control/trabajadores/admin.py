from django.contrib import admin
from .models import Trabajador, RegistroTiempo

@admin.register(Trabajador)
class TrabajadorAdmin(admin.ModelAdmin):
    list_display = ('ID',)  # Mostrar estos campos en la lista de trabajadores en el admin

@admin.register(RegistroTiempo)
class RegistroTiempoAdmin(admin.ModelAdmin):
    list_display = ('trabajador', 'fecha', 'tiempo_entrada', 'tiempo_salida', 'tiempo_trabajado')  # Mostrar estos campos en la lista de registros de tiempo en el admin
    list_filter = ('trabajador', 'fecha')  # Agregar filtros por trabajador y fecha en el admin
