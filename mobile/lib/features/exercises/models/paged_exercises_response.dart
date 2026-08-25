import 'package:gymflow/features/exercises/models/exercise_summary.dart';

class PagedExercisesResponse {
  const PagedExercisesResponse({
    required this.items,
    required this.page,
    required this.pageSize,
    required this.totalCount,
    required this.totalPages,
  });

  final List<ExerciseSummary> items;
  final int page;
  final int pageSize;
  final int totalCount;
  final int totalPages;

  factory PagedExercisesResponse.fromJson(
      Map<String, dynamic> json,
      ) {
    final rawItems = json['items'];

    if (rawItems is! List) {
      throw const FormatException(
        'Invalid exercises response.',
      );
    }

    final items = rawItems.map((item) {
      if (item is! Map) {
        throw const FormatException(
          'Invalid exercise item.',
        );
      }

      return ExerciseSummary.fromJson(
        Map<String, dynamic>.from(item),
      );
    }).toList(growable: false);

    return PagedExercisesResponse(
      items: items,
      page: (json['page'] as num).toInt(),
      pageSize: (json['pageSize'] as num).toInt(),
      totalCount: (json['totalCount'] as num).toInt(),
      totalPages: (json['totalPages'] as num).toInt(),
    );
  }
}
