from django.urls import path
from .views import RunSimuladorView, RunMasDiasView, TrabajadoresConHorasCero, TopTrabajadores, PuntualidadTrabajadores
from .views import HorasTrabajadas, HorasNoTrabajadas, DiaMenosHorasTrabajadas, PromedioHorasTrabajadas, HorasDeAlmuerzo
from .views import DiasTrabajadosAlMes, DiasNoTrabajadosConsecutivos, CalcularSueldo, HorasExtras


urlpatterns = [
    path('runsimulador/', RunSimuladorView.as_view(), name='runSimulador'),
    path('runMasDias/', RunMasDiasView.as_view(), name='runMasDias'),
    path('trabajadoresCeroHoras/',TrabajadoresConHorasCero.as_view(), name='trabajadoresCeroHoras'),
    path('topTrabajadores/', TopTrabajadores.as_view(), name='topTrabajadores'),
    path('puntualidadTrabajadores/', PuntualidadTrabajadores.as_view(), name='puntualidadTrabajadores'),
    path('horasTrabajadas/', HorasTrabajadas.as_view(), name='horasTrabajadas'),
    path('horasNoTrabajadas/', HorasNoTrabajadas.as_view(), name='horasNoTrabajadas'),
    path('diaMenosHorasTrabajadas/', DiaMenosHorasTrabajadas.as_view(), name='diaMenosHorasTrabajadas'),
    path('calcularSueldo/', CalcularSueldo.as_view(), name='calcularSueldo'),
    path('promedioHorasTrabajadas/', PromedioHorasTrabajadas.as_view(), name='promedioHorasTrabajadas'),
    path('horasExtras/',HorasExtras.as_view(),name='horasExtras'),
    path('horasDeAlmuerzo/', HorasDeAlmuerzo.as_view(), name='horasDeAlmuerzo'),
    path('diasTrabajadosAlMes/', DiasTrabajadosAlMes.as_view(), name='diasTrabajadosAlMes'),
    path('diasNoTrabajadosConsecutivos/', DiasNoTrabajadosConsecutivos.as_view(), name='diasNoTrabajadosConsecutivos'),

]