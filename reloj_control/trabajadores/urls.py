from django.urls import path
from .views import RunSimuladorView

urlpatterns = [
    path('runsimulador/', RunSimuladorView.as_view(), name='run-simulador'),
]