import 'package:gymflow/features/workout_templates/models/workout_template_summary.dart';

class PagedWorkoutTemplatesResponse {
  const PagedWorkoutTemplatesResponse({
    required this.items,
    required this.page,
    required this.pageSize,
    required this.totalCount,
    required this.totalPages,
  });

  final List<WorkoutTemplateSummary> items;
  final int page;
  final int pageSize;
  final int totalCount;
  final int totalPages;

  factory PagedWorkoutTemplatesResponse.fromJson(
      Map<String, dynamic> json,
      ) {
    final rawItems = json['items'];

    if (rawItems is! List) {
      throw const FormatException(
        'Invalid workout templates response.',
      );
    }

    final items = rawItems.map((item) {
      if (item is! Map) {
        throw const FormatException(
          'Invalid workout template item.',
        );
      }

      return WorkoutTemplateSummary.fromJson(
        Map<String, dynamic>.from(item),
      );
    }).toList(growable: false);

    return PagedWorkoutTemplatesResponse(
      items: items,
      page: (json['page'] as num).toInt(),
      pageSize: (json['pageSize'] as num).toInt(),
      totalCount: (json['totalCount'] as num).toInt(),
      totalPages: (json['totalPages'] as num).toInt(),
    );
  }
}
