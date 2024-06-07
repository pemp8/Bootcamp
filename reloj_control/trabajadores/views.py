import subprocess
import json
from rest_framework.views import APIView
from rest_framework.response import Response
from rest_framework import status
from .models import Trabajador, RegistroTiempo
from .serializers import TrabajadorSerializer, TrabajadorConHorasCeroSerializer
from datetime import datetime
from datetime import time
import random
from datetime import timedelta
from django.db.models import Sum, F, Avg, Min
from django.utils import timezone
import calendar
from django.shortcuts import render

def index(request):
    return render(request, 'trabajadores/index.html')


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
            # Se obtienen los dias a generar datos
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
                    # Si no hay registros para este trabajador, establecer fecha_maxima = fecha actual
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
        
class TrabajadoresConHorasCero(APIView):
    def get(self, request):
        # Se filtran los registros para obtener los  dias ausentes
        registros_cero = RegistroTiempo.objects.filter(
            tiempo_entrada__hour=0, tiempo_entrada__minute=0, tiempo_entrada__second=0,
            tiempo_salida__hour=0, tiempo_salida__minute=0, tiempo_salida__second=0,
            tiempo_trabajado__hour=0, tiempo_trabajado__minute=0, tiempo_trabajado__second=0
        )
        # Se crea la respuesta
        respuesta = {}
        for registro in registros_cero:
            trabajador_id = registro.trabajador.ID
            if trabajador_id not in respuesta:
                respuesta[trabajador_id] = {
                    "ID": trabajador_id,
                    "dias_cero_horas": []
                }
            respuesta[trabajador_id]["dias_cero_horas"].append(registro.fecha)

        respuesta_list = [v for v in respuesta.values()]
        serializer = TrabajadorConHorasCeroSerializer(respuesta_list, many=True)
        return Response(serializer.data, status=status.HTTP_200_OK)
    
class TopTrabajadores(APIView):
    def get(self, request):
        fecha_inicio = request.query_params.get('fecha_inicio')
        fecha_fin = request.query_params.get('fecha_fin')

        # Validar que ambos parámetros estén presentes
        if not fecha_inicio or not fecha_fin:
            return Response({"error": "Debe proporcionar fecha_inicio y fecha_fin"}, status=status.HTTP_400_BAD_REQUEST)

        # Filtrar registros por el rango de fechas y calcular las horas trabajadas
        registros = RegistroTiempo.objects.filter(fecha__range=[fecha_inicio, fecha_fin])
        trabajadores_horas = registros.values('trabajador').annotate(
            total_minutos=Sum(F('tiempo_trabajado__hour')*60 + F('tiempo_trabajado__minute'))
        ).order_by('-total_minutos')[:5]

        # Crear la respuesta
        top_trabajadores = []
        for idx, th in enumerate(trabajadores_horas, start=1):
            trabajador = Trabajador.objects.get(ID=th['trabajador'])
            trabajador_data = TrabajadorSerializer(trabajador).data
            total_minutos = th['total_minutos']
            horas = total_minutos // 60
            minutos = total_minutos % 60
            trabajador_data['total_horas'] = f"{horas}:{minutos:02d}"  
            trabajador_data['ranking'] = idx  
            top_trabajadores.append(trabajador_data)

        respuesta = {
            "periodo_evaluado": {
                "fecha_inicio": fecha_inicio,
                "fecha_final": fecha_fin
            },
            "top_trabajadores": top_trabajadores
        }

        return Response(respuesta, status=status.HTTP_200_OK)
    
class PuntualidadTrabajadores(APIView):
    def get(self, request):
        
        registros = RegistroTiempo.objects.all()

        # Diccionarios para guardar la puntualidad de entrada y salida
        puntualidad_entrada = {}
        puntualidad_salida = {}

        for registro in registros:
            trabajador = registro.trabajador

            # Calcular la puntualidad de entrada
            hora_entrada_real = registro.tiempo_entrada
            hora_entrada_esperada = time(hour=9, minute=0)  
            if hora_entrada_real > hora_entrada_esperada:
                puntualidad_entrada[trabajador.ID] = puntualidad_entrada.get(trabajador.ID, 0) + 1

            # Calcular la puntualidad de salida
            hora_salida_real = registro.tiempo_salida
            hora_salida_esperada = time(hour=18, minute=0)
            if hora_salida_real < hora_salida_esperada:
                puntualidad_salida[trabajador.ID] = puntualidad_salida.get(trabajador.ID, 0) + 1

        # Crear la respuesta
        avisos_entrada = {}
        avisos_salida = {}
        for trabajador_id, contador_entrada in puntualidad_entrada.items():
            trabajador = Trabajador.objects.get(ID=trabajador_id)
            avisos_entrada[trabajador.ID] = {
                "contador_entrada_tarde": contador_entrada
            }

        for trabajador_id, contador_salida in puntualidad_salida.items():
            trabajador = Trabajador.objects.get(ID=trabajador_id)
            avisos_salida[trabajador.ID] = {
                "contador_salida_temprano": contador_salida
            }

        
        respuesta = {
            "avisos_entrada_tarde": avisos_entrada,
            "avisos_salida_temprano": avisos_salida
        }

        return Response(respuesta, status=status.HTTP_200_OK)
    
class HorasTrabajadas(APIView):
    def get(self, request, *args, **kwargs):
        try:
            # Se obtiene el id del trabajador y el dia a buscar
            trabajador_id = request.query_params.get('trabajador_id')
            fecha = request.query_params.get('fecha')
            
            # Se verifica que ambos parametros sean ingresados
            if not trabajador_id or not fecha:
                return Response({"error": "Se requiere el ID del trabajador y la fecha."}, status=status.HTTP_400_BAD_REQUEST)
            
            # Se obtienen el registro del trabajador
            registro = RegistroTiempo.objects.filter(trabajador_id=trabajador_id, fecha=fecha).first()
            
            if not registro:
                return Response({"error": "No se encontraron registros para el trabajador en la fecha especificada."}, status=status.HTTP_404_NOT_FOUND)
            
            return Response({"horas_trabajadas": registro.tiempo_trabajado}, status=status.HTTP_200_OK)
        
        except Exception as e:
            return Response({"error": str(e)}, status=status.HTTP_500_INTERNAL_SERVER_ERROR)

class HorasNoTrabajadas(APIView):
    def get(self, request, *args, **kwargs):
        try:
            # Se obtiene el id del trabajador y dia a buscar
            trabajador_id = request.query_params.get('trabajador_id')
            fecha = request.query_params.get('fecha')
            
            # Se verifican ambos parametros
            if not trabajador_id or not fecha:
                return Response({"error": "Se requiere el ID del trabajador y la fecha."}, status=status.HTTP_400_BAD_REQUEST)
            
            # Se busca el registro del id y dia especifico
            registro = RegistroTiempo.objects.filter(trabajador_id=trabajador_id, fecha=fecha).first()
            
            if not registro:
                return Response({"error": "No se encontraron registros para el trabajador en la fecha especificada."}, status=status.HTTP_404_NOT_FOUND)
            
            minutos_trabajados = registro.tiempo_trabajado.hour*60 + registro.tiempo_trabajado.minute
            minutos_esperados = 9*60
            minutos_no_trabajados = minutos_esperados - minutos_trabajados
            horas_no_trabajadas = minutos_no_trabajados//60
            minutos_menos_horas = minutos_no_trabajados%60
            
            tiempo_no_trabajado = time(hour=horas_no_trabajadas, minute=minutos_menos_horas)
            
            return Response({"horas_no_trabajadas": tiempo_no_trabajado}, status=status.HTTP_200_OK)
        
        except Exception as e:
            return Response({"error": str(e)}, status=status.HTTP_500_INTERNAL_SERVER_ERROR)

class DiaMenosHorasTrabajadas(APIView):
    def get(self, request, *args, **kwargs):
        try:
            # Se obtiene el ID del trabajador a buscar
            trabajador_id = request.query_params.get('trabajador_id')
            
            if not trabajador_id:
                return Response({"error": "Se requiere el ID del trabajador."}, status=status.HTTP_400_BAD_REQUEST)
            
            # Consulta para obtener el día con el menor tiempo trabajado del trabajador
            menor_tiempo_trabajado = RegistroTiempo.objects.filter(trabajador_id=trabajador_id). \
                                      aggregate(min_tiempo=Min(F('tiempo_trabajado')))
            
            if not menor_tiempo_trabajado['min_tiempo']:
                return Response({"error": "No se encontraron registros de tiempo trabajado para el trabajador especificado."},
                                status=status.HTTP_404_NOT_FOUND)
            
            # Obtener el día con el menor tiempo trabajado
            dia_menor_tiempo = RegistroTiempo.objects.filter(trabajador_id=trabajador_id,
                                                              tiempo_trabajado=menor_tiempo_trabajado['min_tiempo']). \
                                values('fecha', 'tiempo_trabajado').first()
            
            # Convertir las horas trabajadas a formato horas:minutos
            horas_trabajadas = timezone.timedelta(hours=dia_menor_tiempo['tiempo_trabajado'].hour,
                                                   minutes=dia_menor_tiempo['tiempo_trabajado'].minute)
            
            respuesta = {
                "dia_menor_tiempo": {
                    "fecha": dia_menor_tiempo['fecha'],
                    "horas_trabajadas": str(horas_trabajadas)
                }
            }

            return Response(respuesta, status=status.HTTP_200_OK)
        
        except Exception as e:
            return Response({"error": str(e)}, status=status.HTTP_500_INTERNAL_SERVER_ERROR)
        
class CalcularSueldo(APIView):
    def get(self, request, *args, **kwargs):
        try:
            trabajador_id = request.query_params.get('trabajador_id')
            fecha = request.query_params.get('fecha')

            if not trabajador_id or not fecha:
                return Response({"error": "Se requiere el ID del trabajador y la fecha."}, status=status.HTTP_400_BAD_REQUEST)

            registro = RegistroTiempo.objects.filter(trabajador_id=trabajador_id, fecha=fecha).first()

            if not registro:
                return Response({"error": "No se encontraron registros para el trabajador especificado."}, status=status.HTTP_404_NOT_FOUND)

            minutos_trabajados = registro.tiempo_trabajado.hour*60 + registro.tiempo_trabajado.minute
            tiempo_trabajado = minutos_trabajados//60
            horas = registro.tiempo_trabajado.hour

            if minutos_trabajados is None:
                return Response({"error": "No se pudo calcular el tiempo trabajado. "}, status=status.HTTP_500_INTERNAL_SERVER_ERROR)

            sueldo = 100
            sueldo_final_por_horas_trabajadas = tiempo_trabajado*sueldo

            respuesta = {
                "ID": trabajador_id,
                "fecha": fecha,
                "sueldo": sueldo_final_por_horas_trabajadas,
                "horas pagadas":horas
            }

            return Response(respuesta, status=status.HTTP_200_OK)
        
        except Exception as e:
            return Response({"error": str(e)}, status=status.HTTP_500_INTERNAL_SERVER_ERROR)

class PromedioHorasTrabajadas(APIView):
    def get(self, request):
        try:
            # Se obtiene el ID del trabajador y el rango de fechas
            trabajador_id = request.query_params.get('trabajador_id')
            fecha_inicio = request.query_params.get('fecha_inicio')
            fecha_fin = request.query_params.get('fecha_fin')
            
            if not trabajador_id or not fecha_inicio or not fecha_fin:
                return Response({"error": "Se requiere el ID del trabajador, fecha de inicio y fecha de fin."}, status=status.HTTP_400_BAD_REQUEST)
            
            # Convertir las fechas de cadena a objetos datetime
            fecha_inicio_obj = datetime.strptime(fecha_inicio, "%Y-%m-%d")
            fecha_fin_obj = datetime.strptime(fecha_fin, "%Y-%m-%d")
            
            # Consulta para obtener los registros de tiempo trabajado en el periodo especificado
            registros = RegistroTiempo.objects.filter(trabajador_id=trabajador_id, fecha__range=[fecha_inicio, fecha_fin])
            
            if not registros.exists():
                return Response({"error": "No se encontraron registros de tiempo trabajado para el trabajador en el periodo especificado."},
                                status=status.HTTP_404_NOT_FOUND)

            # Calcular el total de horas trabajadas en el periodo
            total_trabajado_en_mins = 0
            for registro in registros:
                entrada = registro.tiempo_entrada.hour*60 + registro.tiempo_entrada.minute
                salida = registro.tiempo_salida.hour*60 + registro.tiempo_salida.minute
                trabajado = salida - entrada
                total_trabajado_en_mins += trabajado

            # Calcular el número de días en el periodo
            num_dias_periodo = (fecha_fin_obj - fecha_inicio_obj).days + 1
            
            # Calcular el promedio de horas trabajadas por día
            promedio_minutos = int(total_trabajado_en_mins / num_dias_periodo)
            promedio_hora = promedio_minutos//60
            promedio_minuto = promedio_minutos%60
            promedio = time(hour=promedio_hora, minute=promedio_minuto)
            
            # Construir la respuesta
            respuesta = {
                "trabajador_id": trabajador_id,
                "fecha_inicio": fecha_inicio,
                "fecha_fin": fecha_fin,
                "promedio_horas_por_dia": promedio
            }
            
            return Response(respuesta, status=status.HTTP_200_OK)
        
        except Exception as e:
            return Response({"error": str(e)}, status=status.HTTP_500_INTERNAL_SERVER_ERROR)

class HorasExtras(APIView):
    def get(self, request, *args, **kwargs):
        try:
            # Se obtiene el ID del trabajador y el rango de fechas
            trabajador_id = request.query_params.get('trabajador_id')
            fecha_inicio = request.query_params.get('fecha_inicio')
            fecha_fin = request.query_params.get('fecha_fin')
            
            if not trabajador_id or not fecha_inicio or not fecha_fin:
                return Response({"error": "Se requiere el ID del trabajador, fecha de inicio y fecha de fin."}, status=status.HTTP_400_BAD_REQUEST)
            
            # Convertir las fechas de cadena a objetos datetime
            fecha_inicio_obj = datetime.strptime(fecha_inicio, "%Y-%m-%d")
            fecha_fin_obj = datetime.strptime(fecha_fin, "%Y-%m-%d")
            
            # Consulta para obtener los registros de tiempo trabajado en el periodo especificado
            registros = RegistroTiempo.objects.filter(trabajador_id=trabajador_id, fecha__range=[fecha_inicio, fecha_fin])
            
            if not registros.exists():
                return Response({"error": "No se encontraron registros de tiempo trabajado para el trabajador en el periodo especificado."},
                                status=status.HTTP_404_NOT_FOUND)

            # Calcular el total de horas trabajadas en el periodo
            total_trabajado_en_mins = 0
            for registro in registros:
                entrada = registro.tiempo_entrada.hour*60 + registro.tiempo_entrada.minute
                salida = registro.tiempo_salida.hour*60 + registro.tiempo_salida.minute
                trabajado = salida - entrada
                total_trabajado_en_mins += trabajado
            
            # Calcular el número de días en el periodo
            num_dias_periodo = (fecha_fin_obj - fecha_inicio_obj).days + 1
            minutos_esperados_periodo = num_dias_periodo*9*60

            if(minutos_esperados_periodo>total_trabajado_en_mins):
                tiempo_extra = time(hour=0,minute=0)
            elif(total_trabajado_en_mins>minutos_esperados_periodo):
                minutos_extras = minutos_esperados_periodo-total_trabajado_en_mins
                hora_extra = minutos_extras//60
                minuto_extra = minutos_extras%60
                tiempo_extra = time(hour=hora_extra,minute=minuto_extra)

            
            respuesta = {
                    "ID": trabajador_id,
                    "fecha_inicio":fecha_inicio,
                    "fecha_final":fecha_fin,
                    "horas_extras":tiempo_extra
                }
            

            return Response(respuesta, status=status.HTTP_200_OK)
        
        except Exception as e:
            return Response({"error": str(e)}, status=status.HTTP_500_INTERNAL_SERVER_ERROR)
        
class HorasDeAlmuerzo(APIView):
    def get(self, request):
        try:
            # Obtener todos los trabajadores
            trabajadores = RegistroTiempo.objects.all()

            # Crear un diccionario para almacenar las horas de almuerzo de todos los trabajadores
            horas_almuerzo = {}

            for trabajador in trabajadores:
                # Si el tiempo de salida es medianoche, saltar al siguiente ciclo del bucle
                if trabajador.tiempo_salida == time(0, 0):
                    continue

                # Generar una hora de inicio de almuerzo aleatoria entre las 13:00 y las 14:00
                inicio_almuerzo = time(hour=random.randint(13, 14), minute=random.randint(0, 59))

                # Generar una hora de fin de almuerzo aleatoria entre la hora de inicio y las 15:00
                fin_almuerzo = time(hour=random.randint(inicio_almuerzo.hour, 15), minute=random.randint(0, 59))

                # Asegurarse de que la hora de fin de almuerzo es después de la hora de inicio de almuerzo
                while fin_almuerzo <= inicio_almuerzo:
                    fin_almuerzo = time(hour=random.randint(inicio_almuerzo.hour, 15), minute=random.randint(0, 59))

                # Calcular el tiempo total de almuerzo
                inicio = datetime.combine(datetime.today(), inicio_almuerzo)
                fin = datetime.combine(datetime.today(), fin_almuerzo)
                total_almuerzo_min = (fin - inicio).seconds // 60  # en minutos

                # Convertir los minutos totales a horas y minutos
                total_almuerzo = time(hour=total_almuerzo_min // 60, minute=total_almuerzo_min % 60)

                # Crear un diccionario para este día de trabajo
                dia_trabajo = {"Fecha": trabajador.fecha, "Inicio del almuerzo": inicio_almuerzo, "Fin del almuerzo": fin_almuerzo, "Tiempo total de almuerzo": total_almuerzo}

                # Si este trabajador ya tiene entradas en el diccionario, agregar este día de trabajo a la lista existente
                if trabajador.trabajador_id in horas_almuerzo:
                    horas_almuerzo[trabajador.trabajador_id].append(dia_trabajo)
                # Si no, crear una nueva lista para este trabajador
                else:
                    horas_almuerzo[trabajador.trabajador_id] = [dia_trabajo]

            return Response(horas_almuerzo, status=status.HTTP_200_OK)
        except Exception as e:
            return Response({"error": str(e)}, status=status.HTTP_500_INTERNAL_SERVER_ERROR)
        
class DiasTrabajadosAlMes(APIView):
    def get(self, request):
        try:
            # Obtener todos los trabajadores
            trabajadores = RegistroTiempo.objects.all()

            # Crear un diccionario para almacenar los días trabajados por cada trabajador
            dias_trabajados = {}

            for trabajador in trabajadores:
                # Si el tiempo de salida es medianoche, saltar al siguiente ciclo del bucle
                if trabajador.tiempo_salida == time(0, 0):
                    continue

                # Extraer el mes y el año de la fecha del trabajador
                year = trabajador.fecha.year
                month = trabajador.fecha.month

                # Transformar el número del mes en el nombre del mes
                month_name = calendar.month_name[month]

                # Crear un diccionario para almacenar los días trabajados por cada trabajador en este mes y año
                if trabajador.trabajador_id not in dias_trabajados:
                    dias_trabajados[trabajador.trabajador_id] = {}

                if year not in dias_trabajados[trabajador.trabajador_id]:
                    dias_trabajados[trabajador.trabajador_id][year] = {}

                if month_name not in dias_trabajados[trabajador.trabajador_id][year]:
                    dias_trabajados[trabajador.trabajador_id][year][month_name] = set()

                # Agregar la fecha del registro al conjunto de fechas
                dias_trabajados[trabajador.trabajador_id][year][month_name].add(trabajador.fecha.day)

            # Convertir los conjuntos de fechas en conteos de días trabajados
            for trabajador_id, years in dias_trabajados.items():
                for year, months in years.items():
                    for month_name, days in months.items():
                        dias_trabajados_en_mes = len(days)
                        dias_no_trabajados = calendar.monthrange(year, list(calendar.month_name).index(month_name))[1] - dias_trabajados_en_mes
                        dias_trabajados[trabajador_id][year][month_name] = f"{dias_trabajados_en_mes} días trabajados, {dias_no_trabajados} días no trabajados"

            return Response(dias_trabajados, status=status.HTTP_200_OK)
        except Exception as e:
            return Response({"error": str(e)}, status=status.HTTP_500_INTERNAL_SERVER_ERROR)

class DiasNoTrabajadosConsecutivos(APIView):
    def get(self, request):
        trabajadores = RegistroTiempo.objects.all().order_by("fecha")
        # Crear un diccionario para almacenar los días no trabajados consecutivos de cada trabajador
        dias_no_trabajados = {trabajador.trabajador_id: [] for trabajador in trabajadores}
        dias_no_trabajados_final = {trabajador.trabajador_id: [] for trabajador in trabajadores}

        for trabajador in trabajadores:
            if trabajador.tiempo_salida == time(0, 0):
                dias_no_trabajados[trabajador.trabajador_id].append(trabajador.fecha)
        
        # Si hay menos de 3 items guardados en la lista, eliminar el trabajador del diccionario
        dias_no_trabajados = {trabajador_id: dias for trabajador_id, dias in dias_no_trabajados.items() if len(dias) >= 3}

        # Chequea si los 3 dias almacenados en la lista son consecutivos tipo de guardado es ("2024-06-10").
        for trabajador_id, dias in dias_no_trabajados.items():
            i = 0
            while i < len(dias) - 2:
                if (dias[i + 1] - dias[i]).days == 1 and (dias[i + 2] - dias[i + 1]).days == 1:
                    dias_no_trabajados_final[trabajador_id].append(
                        f"Faltó al trabajo consecutivamente los días: {dias[i].strftime('%Y-%m-%d')}, {dias[i + 1].strftime('%Y-%m-%d')}, {dias[i + 2].strftime('%Y-%m-%d')}"
                    )
                    i += 3  # Saltar los 3 días ya procesados
                else:
                    i += 1

        # Si la lista está vacia, eliminar el trabajador del diccionario
        dias_no_trabajados_final = {trabajador_id: dias for trabajador_id, dias in dias_no_trabajados_final.items() if dias}

        return Response(dias_no_trabajados_final)
    

class DiaConMasHoras(APIView): # Se le puede agregar tambien el mes
    def get(self, request):
        trabajadores = RegistroTiempo.objects.all()
        # Crear un diccionario para almacenar los días y las horas trabajadas por cada trabajador
        horas_trabajadas = {trabajador.trabajador_id: [] for trabajador in trabajadores}
        horas_maximas_trabajadas = {trabajador.trabajador_id: [] for trabajador in trabajadores}

        for trabajador in trabajadores:
            horas_trabajadas[trabajador.trabajador_id].append((trabajador.tiempo_trabajado, trabajador.fecha))

        for i in horas_trabajadas:
            horas_maximas_trabajadas[i] = max(horas_trabajadas[i])
        return Response(horas_maximas_trabajadas)
    
class AusenciasJustificadas(APIView):
    def get(self, request):
        # Se seleccion una valor random de 1 a 3, si es 1 se le asigna una ausencia justificada
        trabajadores = RegistroTiempo.objects.all()
        ausencias_justificadas = {trabajador.trabajador_id: [] for trabajador in trabajadores}
        for trabajador in trabajadores:
            if random.randint(1, 3) == 1:
                if trabajador.tiempo_salida == time(0, 0):
                    ausencias_justificadas[trabajador.trabajador_id].append(f"{trabajador.fecha.strftime('%Y-%m-%d')}: Ausencia justificada")

        # Eliminar los trabajadores que no tienen ausencias justificadas
        ausencias_justificadas = {trabajador_id: dias for trabajador_id, dias in ausencias_justificadas.items() if dias}
        return Response(ausencias_justificadas)

class IdentifTurnosFDS(APIView):
    def get(self, request):
        trabajadores = RegistroTiempo.objects.all()
        turnos_fds = {trabajador.trabajador_id: [] for trabajador in trabajadores}

        for trabajador in trabajadores:
            if trabajador.fecha.isoweekday() >= 6:
                if trabajador.tiempo_salida != time(0, 0):
                    turnos_fds[trabajador.trabajador_id].append(f"Trabajó el fin de semana: {trabajador.fecha.strftime('%Y-%m-%d')}")

        turnos_fds = {trabajador_id: dias for trabajador_id, dias in turnos_fds.items() if dias}
        return Response(turnos_fds)