import 'dart:convert';
import 'dart:typed_data';

import 'package:file_saver/file_saver.dart';
import 'package:gymflow/features/workouts/models/workout_details.dart';

class WorkoutExportService {
  Future<void> exportCsv({
    required WorkoutDetails workout,
    required String studentName,
  }) async {
    final buffer = StringBuffer();

    buffer.writeln(
      'Aluno;Treino;Dia;Exercício;Grupo muscular;'
          'Séries;Repetições;Descanso (s);Observações',
    );

    final days = [...workout.days]
      ..sort((a, b) => a.order.compareTo(b.order));

    for (final day in days) {
      final exercises = [...day.exercises]
        ..sort((a, b) => a.order.compareTo(b.order));

      for (final exercise in exercises) {
        buffer.writeln([
          studentName,
          workout.name,
          day.name,
          exercise.exerciseName,
          exercise.muscleGroup,
          exercise.sets,
          exercise.repetitions,
          exercise.restSeconds ?? '',
          exercise.notes ?? '',
        ].map(_escape).join(';'));
      }
    }

    final bytes = Uint8List.fromList(
      utf8.encode('\uFEFF${buffer.toString()}'),
    );

    await FileSaver.instance.saveAs(
      name: _fileName(studentName, workout.name),
      bytes: bytes,
      fileExtension: 'csv',
      mimeType: MimeType.csv,
    );
  }

  String _escape(Object value) {
    final text = value.toString().replaceAll('"', '""');

    return '"$text"';
  }

  String _fileName(String studentName, String workoutName) {
    final raw = 'treino_${studentName}_$workoutName'
        .toLowerCase()
        .replaceAll(RegExp(r'[^a-z0-9]+'), '_')
        .replaceAll(RegExp(r'^_+|_+$'), '');

    return raw.isEmpty ? 'treino' : raw;
  }
}
