import 'package:gymflow/features/workouts/models/workout_history_item.dart';

class PagedWorkoutHistoryResponse {
  const PagedWorkoutHistoryResponse({
    required this.items,
    required this.page,
    required this.pageSize,
    required this.totalCount,
    required this.totalPages,
  });

  final List<WorkoutHistoryItem> items;
  final int page;
  final int pageSize;
  final int totalCount;
  final int totalPages;

  factory PagedWorkoutHistoryResponse.fromJson(
      Map<String, dynamic> json,
      ) {
    final rawItems = json['items'];

    if (rawItems is! List) {
      throw const FormatException(
        'Invalid workout history response.',
      );
    }

    return PagedWorkoutHistoryResponse(
      items: rawItems
          .map(
            (item) => WorkoutHistoryItem.fromJson(
          Map<String, dynamic>.from(
            item as Map,
          ),
        ),
      )
          .toList(growable: false),
      page: (json['page'] as num).toInt(),
      pageSize:
      (json['pageSize'] as num).toInt(),
      totalCount:
      (json['totalCount'] as num).toInt(),
      totalPages:
      (json['totalPages'] as num).toInt(),
    );
  }
}
