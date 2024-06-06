from rest_framework import serializers
from .models import Trabajador, RegistroTiempo

class TrabajadorSerializer(serializers.ModelSerializer):
    class Meta:
        model = Trabajador
        fields = '__all__'

class RegistroTiempoSerializer(serializers.ModelSerializer):
    class Meta:
        model = RegistroTiempo
        fields = '__all__'

class TrabajadorConHorasCeroSerializer(serializers.Serializer):
    ID = serializers.IntegerField()
    dias_cero_horas = serializers.ListField(child=serializers.DateField())