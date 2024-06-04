import subprocess
import json
from rest_framework.views import APIView
from rest_framework.response import Response
from rest_framework import status
from .models import Trabajador
from .serializers import TrabajadorSerializer
import datetime
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
                if 'ausente' in trabajador_data and trabajador_data['ausente']:
                    continue
                
                serializer = TrabajadorSerializer(data=trabajador_data)
                if serializer.is_valid():
                    serializer.save()
                else:
                    return Response(serializer.errors, status=status.HTTP_400_BAD_REQUEST)
            
            return Response({"message": "Simulador ejecutado y datos almacenados"}, status=status.HTTP_200_OK)
        
        except Exception as e:
            return Response({"error": str(e)}, status=status.HTTP_500_INTERNAL_SERVER_ERROR)
        
