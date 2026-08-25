import 'package:gymflow/features/students/models/student_summary.dart';

class PagedStudentsResponse {
  const PagedStudentsResponse({
    required this.items,
    required this.page,
    required this.pageSize,
    required this.totalCount,
    required this.totalPages,
  });

  final List<StudentSummary> items;
  final int page;
  final int pageSize;
  final int totalCount;
  final int totalPages;

  factory PagedStudentsResponse.fromJson(
      Map<String, dynamic> json,
      ) {
    final rawItems = json['items'];

    if (rawItems is! List) {
      throw const FormatException(
        'Invalid students response: items is not a list.',
      );
    }

    final items = rawItems.map((item) {
      if (item is! Map) {
        throw const FormatException(
          'Invalid student item.',
        );
      }

      return StudentSummary.fromJson(
        Map<String, dynamic>.from(item),
      );
    }).toList(growable: false);

    return PagedStudentsResponse(
      items: items,
      page: (json['page'] as num).toInt(),
      pageSize: (json['pageSize'] as num).toInt(),
      totalCount: (json['totalCount'] as num).toInt(),
      totalPages: (json['totalPages'] as num).toInt(),
    );
  }
}
