# Generated by Django 5.0.6 on 2024-06-04 19:33

from django.db import migrations, models


class Migration(migrations.Migration):

    initial = True

    dependencies = [
    ]

    operations = [
        migrations.CreateModel(
            name='Trabajador',
            fields=[
                ('ID', models.IntegerField(primary_key=True, serialize=False)),
                ('fecha', models.DateField()),
                ('tiempo_entrada', models.TimeField()),
                ('tiempo_salida', models.TimeField()),
                ('tiempo_trabajado', models.DecimalField(decimal_places=2, max_digits=5)),
            ],
        ),
    ]
