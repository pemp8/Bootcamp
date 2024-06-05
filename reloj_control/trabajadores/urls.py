from django.urls import path
from .views import RunSimuladorView, RunMasDiasView

urlpatterns = [
    path('runsimulador/', RunSimuladorView.as_view(), name='run-simulador'),
    path('runMasDias/', RunMasDiasView.as_view(), name='runMasDias'),
]