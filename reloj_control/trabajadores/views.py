import subprocess
import json
from rest_framework.views import APIView
from rest_framework.response import Response
from rest_framework import status
from .models import Trabajador, RegistroTiempo
from .serializers import TrabajadorSerializer
import datetime
from datetime import datetime
from datetime import time
import random
from datetime import timedelta

class RunSimuladorView(APIView):
    def get(self, request, *args, **kwargs):
        try:
            simulador_path = './trabajadores/bin/simReloj'
            
            result = subprocess.run([simulador_path], capture_output=True, text=True)
            
            if result.returncode != 0:
                return Response({"error": "Error al ejecutar el simulador"}, status=status.HTTP_500_INTERNAL_SERVER_ERROR)

            output = result.stdout
            
            trabajadores_data = json.loads(output)
            for trabajador_data in trabajadores_data:
                
                trabajador_id = trabajador_data['ID']
                fecha_str = trabajador_data['fecha']
                tiempo_entrada_str = trabajador_data['tiempo_entrada']
                tiempo_salida_str = trabajador_data['tiempo_salida']

                # Convertir las cadenas de tiempo a objetos time
                fecha = datetime.strptime(fecha_str, '%Y-%m-%d').date()
                tiempo_entrada = datetime.strptime(tiempo_entrada_str, '%H:%M').time()
                tiempo_salida = datetime.strptime(tiempo_salida_str, '%H:%M').time()

                
                trabajador, created = Trabajador.objects.get_or_create(ID=trabajador_id)
                RegistroTiempo.objects.create(
                    trabajador_id=trabajador_id,
                    fecha=fecha,
                    tiempo_entrada=tiempo_entrada,
                    tiempo_salida=tiempo_salida,
                )
            
            return Response({"message": "Simulador ejecutado y datos almacenados"}, status=status.HTTP_200_OK)
        
        except Exception as e:
            return Response({"error": str(e)}, status=status.HTTP_500_INTERNAL_SERVER_ERROR)

        

class RunMasDiasView(APIView):
    def get(self, request, *args, **kwargs):
        try:
            n_dias = int(request.query_params.get('dias', 0))
            if n_dias <= 0:
                return Response({"error": "Número de días debe ser mayor que cero."}, status=status.HTTP_400_BAD_REQUEST)
            
            trabajadores = Trabajador.objects.all()

            if not trabajadores:
                return Response({"error": "No hay trabajadores en la base de datos."}, status=status.HTTP_404_NOT_FOUND)
            
            for trabajador in trabajadores:
                registros = RegistroTiempo.objects.filter(trabajador=trabajador).order_by('-fecha')
                if registros.exists():
                    fecha_maxima = registros.first().fecha
                else:
                    # Si no hay registros para este trabajador, establecer fecha_maxima a la fecha actual
                    fecha_maxima = datetime.now()
                for i in range(1, n_dias + 1):
                    nueva_fecha = fecha_maxima + timedelta(days=i)

                    # Verificar si ya existe una entrada para este trabajador en esta fecha
                    if not RegistroTiempo.objects.filter(trabajador_id=trabajador.ID, fecha=nueva_fecha).exists():
                        tiempo_entrada = time(hour=random.randint(8, 11), minute=random.randint(0, 59))
                        tiempo_salida = time(hour=random.randint(16, 19), minute=random.randint(0, 59))

                        RegistroTiempo.objects.create(
                            trabajador_id=trabajador.ID,
                            fecha=nueva_fecha,
                            tiempo_entrada=tiempo_entrada,
                            tiempo_salida=tiempo_salida,
                        )
            
            return Response({"message": f"Datos generados para {n_dias} días para todas las IDs existentes."}, status=status.HTTP_200_OK)
        
        except Exception as e:
            return Response({"error": str(e)}, status=status.HTTP_500_INTERNAL_SERVER_ERROR)