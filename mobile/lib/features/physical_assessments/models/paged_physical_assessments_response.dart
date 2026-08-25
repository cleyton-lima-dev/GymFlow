import 'package:gymflow/features/physical_assessments/models/physical_assessment_history_item.dart';

class PagedPhysicalAssessmentsResponse {
  const PagedPhysicalAssessmentsResponse({
    required this.items,
    required this.page,
    required this.pageSize,
    required this.totalCount,
    required this.totalPages,
  });

  final List<PhysicalAssessmentHistoryItem> items;
  final int page;
  final int pageSize;
  final int totalCount;
  final int totalPages;

  factory PagedPhysicalAssessmentsResponse.fromJson(
      Map<String, dynamic> json,
      ) {
    final rawItems = json['items'];

    if (rawItems is! List) {
      throw const FormatException(
        'Invalid physical assessments response.',
      );
    }

    final items = rawItems.map((item) {
      if (item is! Map) {
        throw const FormatException(
          'Invalid physical assessment item.',
        );
      }

      return PhysicalAssessmentHistoryItem.fromJson(
        Map<String, dynamic>.from(item),
      );
    }).toList(growable: false);

    return PagedPhysicalAssessmentsResponse(
      items: items,
      page: (json['page'] as num).toInt(),
      pageSize: (json['pageSize'] as num).toInt(),
      totalCount: (json['totalCount'] as num).toInt(),
      totalPages: (json['totalPages'] as num).toInt(),
    );
  }
}
