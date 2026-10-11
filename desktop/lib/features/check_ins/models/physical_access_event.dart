class PhysicalAccessEvent {
  const PhysicalAccessEvent({
    required this.id,
    required this.requestId,
    required this.studentId,
    required this.studentName,
    required this.decision,
    required this.reason,
    required this.source,
    required this.occurredAt,
    required this.processedAt,
  });

  final String id;
  final String requestId;
  final String? studentId;
  final String? studentName;
  final int decision;
  final int reason;
  final int source;
  final DateTime occurredAt;
  final DateTime processedAt;

  factory PhysicalAccessEvent.fromJson(Map<String, dynamic> json) {
    int readEnum(String key) {
      final value = json[key];

      if (value is int) return value;

      throw FormatException('Invalid physical access event: $key.');
    }

    return PhysicalAccessEvent(
      id: json['id'] as String,
      requestId: json['requestId'] as String,
      studentId: json['studentId'] as String?,
      studentName: json['studentName'] as String?,
      decision: readEnum('decision'),
      reason: readEnum('reason'),
      source: readEnum('source'),
      occurredAt: DateTime.parse(json['occurredAt'] as String),
      processedAt: DateTime.parse(json['processedAt'] as String),
    );
  }
}

class PagedPhysicalAccessEventsResponse {
  const PagedPhysicalAccessEventsResponse({
    required this.items,
    required this.page,
    required this.pageSize,
    required this.totalCount,
    required this.totalPages,
  });

  final List<PhysicalAccessEvent> items;
  final int page;
  final int pageSize;
  final int totalCount;
  final int totalPages;

  factory PagedPhysicalAccessEventsResponse.fromJson(
    Map<String, dynamic> json,
  ) {
    final rawItems = json['items'];

    if (rawItems is! List) {
      throw const FormatException('Invalid physical access events response.');
    }

    return PagedPhysicalAccessEventsResponse(
      items: rawItems
          .map((item) {
            if (item is! Map) {
              throw const FormatException(
                'Invalid physical access event item.',
              );
            }

            return PhysicalAccessEvent.fromJson(
              Map<String, dynamic>.from(item),
            );
          })
          .toList(growable: false),
      page: (json['page'] as num).toInt(),
      pageSize: (json['pageSize'] as num).toInt(),
      totalCount: (json['totalCount'] as num).toInt(),
      totalPages: (json['totalPages'] as num).toInt(),
    );
  }
}
