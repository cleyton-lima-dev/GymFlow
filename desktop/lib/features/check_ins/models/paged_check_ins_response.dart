import 'package:avelri_gestao/features/check_ins/models/check_in_history_item.dart';

class PagedCheckInsResponse {
  const PagedCheckInsResponse({
    required this.items,
    required this.page,
    required this.pageSize,
    required this.totalCount,
    required this.totalPages,
  });

  final List<CheckInHistoryItem> items;
  final int page;
  final int pageSize;
  final int totalCount;
  final int totalPages;

  factory PagedCheckInsResponse.fromJson(Map<String, dynamic> json) {
    final rawItems = json['items'];

    if (rawItems is! List) {
      throw const FormatException(
        'Invalid check-ins response: items is not a list.',
      );
    }

    final items = rawItems
        .map((item) {
          if (item is! Map) {
            throw const FormatException('Invalid check-in item.');
          }

          return CheckInHistoryItem.fromJson(Map<String, dynamic>.from(item));
        })
        .toList(growable: false);

    return PagedCheckInsResponse(
      items: items,
      page: (json['page'] as num).toInt(),
      pageSize: (json['pageSize'] as num).toInt(),
      totalCount: (json['totalCount'] as num).toInt(),
      totalPages: (json['totalPages'] as num).toInt(),
    );
  }
}
